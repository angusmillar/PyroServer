using System.Collections.ObjectModel;
using Abm.Pyro.Application.Cache;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Tenant;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirSubscriptions;

public class FhirSubscriptionService(
    IDateTimeProvider dateTimeProvider,
    IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IFhirUriFactory fhirUriFactory,
    ISearchQueryService searchQueryService,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport,
    IResourceStoreSearch resourceStoreSearch
    ) : IFhirSubscriptionService

{
    private readonly ICollection<OperationOutcome> _operationOutcomeList = new Collection<OperationOutcome>();
    
    public async Task<AcceptSubscriptionOutcome> CanSubscriptionBeAccepted(Subscription subscription)
    {
        if (InValidSubscriptionStatus(subscription) ||
            InValidSubscriptionEndDate(subscription.End) ||
            InValidChannelType(subscription) ||
            InvalidPayloadType(subscription.Channel.Payload))
        {
            return FailedSubscriptionOutcome();
        }

        FhirUri? criteriaFhirUri = ParsesSubscriptionCriteria(subscription.Criteria);
        if (criteriaFhirUri is null)
        {
            return FailedSubscriptionOutcome();
        }

        
        
        FhirResourceTypeId? criteriaResourceEndpoint = ValidateCriteriaTargetResource(criteriaFhirUri.ResourceName);
        if (criteriaResourceEndpoint is null)
        {
            return FailedSubscriptionOutcome();
        }


        if (InValidEndpointPolicyDisallowsSearch(criteriaFhirUri.ResourceName))
        {
            return FailedSubscriptionOutcome();
        }
        
        if (InValidSubscriptionCriteria(subscription.Channel.Endpoint, criteriaFhirUri))
        {
            return FailedSubscriptionOutcome();
        }
        
        SearchQueryServiceOutcome searchQueryServiceOutcome =
            await searchQueryService.Process(criteriaResourceEndpoint.Value, criteriaFhirUri.Query);

        
        if (InValidSubscriptionCriteriaSearchParameters(searchQueryServiceOutcome))
        {
            return FailedSubscriptionOutcome();
        }
        
        //perform the search on the database, only to confirm it works before we set the Subscription to Active
        await resourceStoreSearch.GetSearchTotalCount(searchQueryServiceOutcome);

        subscription.Status = Subscription.SubscriptionStatus.Active;
        
        return new AcceptSubscriptionOutcome(Success: true);
    }

    private bool InValidSubscriptionCriteria(
        string endpoint, 
        FhirUri criteriaFhirUri)
    {
        if (criteriaFhirUri.IsRelativeToServer)
        {
            _operationOutcomeList.Add(operationOutcomeSupport.GetError([
                $"Could not activate the FHIR Subscription because the channel endpoint " +
                $"was equal to the server's own Service Base URL, Type is rest-hook, and Payload is populated. " +
                $"This would create a circular reference and infinite FHIR notification cycles."
            ]));
            return true;
            
        }

        return false;
    }

    private bool InvalidPayloadType(string channelPayload)
    {
        if (string.IsNullOrWhiteSpace(channelPayload))
        {
            return false;
        }

        if (channelPayload.Equals(FhirFormatType.Json.GetDescription(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        
        return true;
    }

    private AcceptSubscriptionOutcome FailedSubscriptionOutcome()
    {
        return new AcceptSubscriptionOutcome(
            Success: false,
            OperationOutcome: operationOutcomeSupport.MergeOperationOutcomeList(_operationOutcomeList));
    }

    private bool InValidChannelType(Subscription subscription)
    {
        if (subscription.Channel.Type.Equals(Subscription.SubscriptionChannelType.RestHook))
        {
            return false;
        }

        _operationOutcomeList.Add(operationOutcomeSupport.GetError([
            $"Could not activate the FHIR Subscription because the channel type of " +
            $"{subscription.Channel.Type.GetLiteral()} is not supported by this server. " +
            $"Only the type of {Subscription.SubscriptionChannelType.RestHook.GetLiteral()} is supported"
        ]));

        return true;
    }

    private bool InValidSubscriptionEndDate(DateTimeOffset? subscriptionEndDate)
    {
        if (!IsSubscriptionEndDated(subscriptionEndDate))
        {
            return false;
        }

        _operationOutcomeList.Add(operationOutcomeSupport.GetError([
            $"Could not activate the FHIR Subscription because its nominated fixed end date date has past"
        ]));

        return true;
    }

    private bool InValidSubscriptionStatus(Subscription subscription)
    {
        if (subscription.Status.Equals(Subscription.SubscriptionStatus.Requested))
        {
            return false;
        }

        _operationOutcomeList.Add(operationOutcomeSupport.GetError([
            "Only Subscription resource with a status of 'requested' can be Created. Only the server can set their " +
            "status  to 'active', 'error' or 'off', based on the server's processing rules"
        ]));

        return true;
    }

    private bool InValidSubscriptionCriteriaSearchParameters(SearchQueryServiceOutcome searchQueryServiceOutcome)
    {
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            SearchQueryServiceOutcome: searchQueryServiceOutcome,
            Headers: new Dictionary<string, StringValues>()));
        
        if (!searchQueryValidatorResult.IsValid)
        {
            _operationOutcomeList.Add(operationOutcomeSupport.GetError([
                "Could not activate the FHIR Subscription because its criteria has invalid search " +
                "parameters."
            ]));

            return true;
        }
        
        return false;
    }
    
    private bool InValidEndpointPolicyDisallowsSearch(string criteriaResourceName)
    {
        if (endpointPolicyService.GetEndpointPolicy(criteriaResourceName).AllowSearch)
        {
            return false;
        }

        _operationOutcomeList.Add(operationOutcomeSupport.GetError([
            "The server's endpoint policy controls have refused to authorize this Subscription activation " +
            "request"
        ]));

        return true;
    }

    private FhirResourceTypeId? ValidateCriteriaTargetResource(string criteriaResourceName)
    {
        FhirResourceTypeId? fhirResourceType = fhirResourceTypeSupport.TryGetResourceType(criteriaResourceName);
        if (fhirResourceType is not null)
        {
            return fhirResourceType.Value;
        }

        _operationOutcomeList.Add(operationOutcomeSupport.GetError([
            $"Could not activate the FHIR Subscription because no valid Resource endpoint could be found within the " +
            $"Subscription's criteria. Found Resource type: {criteriaResourceName}"
        ]));

        return null;
    }

    private FhirUri? ParsesSubscriptionCriteria(string criteria)
    {
        if (fhirUriFactory.TryParse(criteria, out FhirUri? fhirUri, out string errorMessage))
        {
            return fhirUri;
        }

        _operationOutcomeList.Add(operationOutcomeSupport.GetError([
            $"Could not activate the FHIR Subscription because its criteria could not be parsed. {errorMessage}"
        ]));

        return null;
    }

    private bool IsSubscriptionEndDated(DateTimeOffset? subscriptionEnd)
    {
        return subscriptionEnd.HasValue && subscriptionEnd.Value.UtcDateTime < dateTimeProvider.Now.UtcDateTime;
    }
    
}