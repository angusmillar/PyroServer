using Abm.Pyro.Application.Notification;
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.FhirSubscriptions;

public class FhirNotificationService : IFhirNotificationService
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
        //Do the search for the resource adding its ResourceStoreId into the search
        await Task.Delay(10000);
        throw new NotImplementedException();
    }
}