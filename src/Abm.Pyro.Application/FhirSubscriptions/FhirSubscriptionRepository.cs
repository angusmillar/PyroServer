using Abm.Pyro.Application.Cache;
using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Tenant;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;

namespace Abm.Pyro.Application.FhirSubscriptions;

public class FhirSubscriptionRepository(
    ILogger<FhirSubscriptionRepository> logger,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IFhirDeSerializationSupport fhirDeSerializationSupport,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    IFhirUriFactory fhirUriFactory
    ) : IFhirSubscriptionRepository
{
    public async Task<ICollection<ActiveSubscription>> GetActiveSubscriptionList(CancellationToken cancellationToken)
    {
        
        SearchQueryServiceOutcome searchQueryServiceOutcome =
            await searchQueryService.Process(FhirResourceTypeId.Subscription,  queryString: "status=active");


        ResourceStoreSearchOutcome resourceStoreSearchOutcome =
            await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);

        var activeSubscriptions = new List<ActiveSubscription>();
        foreach (var resourceStore in resourceStoreSearchOutcome.ResourceStoreList)
        {
            Resource? resource = fhirDeSerializationSupport.ToResource(resourceStore.Json);
            if (resource is not Subscription subscription)
            {
                throw new InvalidCastException(nameof(resource));
            }
            
            FhirUri criteriaFhirUri = ParsesActiveSubscriptionCriteria(subscription.Id, subscription.Criteria);

            FhirResourceTypeId fhirResourceType =
                fhirResourceTypeSupport.GetRequiredFhirResourceType(criteriaFhirUri.ResourceName);

            activeSubscriptions.Add(new ActiveSubscription(
                ResourceStoreId: resourceStore.ResourceStoreId!.Value,
                ResourceId: resourceStore.ResourceId,
                VersionId: resourceStore.VersionId,
                CriteriaResourceType: fhirResourceType,
                CriteriaQuery: criteriaFhirUri.Query,
                Endpoint: new Uri(subscription.Channel.Endpoint),
                Payload: subscription.Channel.Payload,
                Headers: subscription.Channel.Header.ToArray(),
                EndDateTime: subscription.End));
        }

        return activeSubscriptions;
    }
    private FhirUri ParsesActiveSubscriptionCriteria(string subscriptionResourceId, string criteria)
    {
        if (!fhirUriFactory.TryParse(criteria, out FhirUri? fhirUri, out string errorMessage))
        {
            logger.LogCritical(
                "The active FHIR Subscription with resource id {SubscriptionId} has a criteria that could not be parsed. {ErrorMessage}",
                subscriptionResourceId, errorMessage);
            throw new ApplicationException(
                $"The active FHIR Subscription with resource id {subscriptionResourceId} has a criteria that could not be parsed. {errorMessage}");
        }

        return fhirUri;
    }
    
}