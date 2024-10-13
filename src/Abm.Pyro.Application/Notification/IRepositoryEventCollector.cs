using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.Notification;

public interface IRepositoryEventCollector
{
    void Add(FhirResourceTypeId resourceType, string requestId, RepositoryEventType repositoryEventType, string resourceId);
    void Add(RepositoryEvent repositoryEvent);
    
    public IReadOnlyCollection<RepositoryEvent> RepositoryEventList { get; }
    
    public void Clear();
}