using Abm.Pyro.Application.DependencyFactory;
using Abm.Pyro.Application.FhirClient;
using Abm.Pyro.Application.Notification;
using Abm.Pyro.Application.SearchQuery;
using Abm.Pyro.Application.Validation;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.FhirSubscriptions;

public class FhirNotificationService(
    ILogger<FhirNotificationService> logger,
    IValidator validator,
    IFhirResourceTypeSupport fhirResourceTypeSupport,
    ISearchQueryService searchQueryService,
    IResourceStoreSearch resourceStoreSearch,
    IFhirHttpClientFactory fhirHttpClientFactory) : IFhirNotificationService
{
    public async Task ProcessEventList(ICollection<RepositoryEvent> repositoryEventList)
    {
        if (repositoryEventList.Count == 0)
        {
            return;
        }
        
        //The set of RepositoryEvents in the collection represents all events from a single inbound request on the
        //FHIR API, they MUST all have the same RequestId
        ThrowIfInvalidRequestIds(repositoryEventList);
        
        var fhirNotifiableEventList = GetFhirNotifiableEventList(repositoryEventList);
        
        
        foreach (RepositoryEvent fhirNotifiableEvent in fhirNotifiableEventList)
        {
            logger.LogDebug("FhirNotificationService: {@RepositoryEvent}", fhirNotifiableEvent);
            await ProcessEvent(fhirNotifiableEvent);
        }
        
    }

    private static void ThrowIfInvalidRequestIds(ICollection<RepositoryEvent> repositoryEventList)
    {
        if (!repositoryEventList.All(x => x.RequestId.Equals(repositoryEventList.First().RequestId)))
        {
            throw new ApplicationException("All Repository Events in the collection must have the same RequestId");
        }
    }

    private static IEnumerable<RepositoryEvent> GetFhirNotifiableEventList(ICollection<RepositoryEvent> repositoryEventList)
    {
        RepositoryEventType[] fhirNotifiableEvents = [RepositoryEventType.Create, RepositoryEventType.Update];
        return repositoryEventList.Where(x => fhirNotifiableEvents.Contains(x.RepositoryEventType));
    }

    private async Task ProcessEvent(RepositoryEvent repositoryEvent)
    {
        
        FhirResourceTypeId fhirResourceType = fhirResourceTypeSupport.GetRequiredFhirResourceType("Patient");
    
        SearchQueryServiceOutcome searchQueryServiceOutcome = await searchQueryService.Process(fhirResourceType, "family=Donald&given=Duck");
        ValidatorResult searchQueryValidatorResult = validator.Validate(new SearchQueryServiceOutcomeAndHeaders(
            searchQueryServiceOutcome: searchQueryServiceOutcome, 
            headers: new Dictionary<string, StringValues>()));
        if (!searchQueryValidatorResult.IsValid)
        {
            throw new NotImplementedException();
        }
   
        ResourceStoreSearchOutcome resourceStoreSearchOutcome = await resourceStoreSearch.GetSearch(searchQueryServiceOutcome);
        
        logger.LogDebug("ResourceStoreList.count: {Count}", resourceStoreSearchOutcome.ResourceStoreList.Count);
        
        
        
        
        
        Hl7.Fhir.Rest.FhirClient fhirClient = fhirHttpClientFactory.CreateClient(baseUrl: "https://localhost:7081/pyro");

        CapabilityStatement capabilityStatement = await fhirClient.CapabilityStatementAsync();
        logger.LogDebug("capabilityStatement.name: {Name}", capabilityStatement.Name);
        
        //Do the search for the resource adding its ResourceStoreId into the search
    }
}