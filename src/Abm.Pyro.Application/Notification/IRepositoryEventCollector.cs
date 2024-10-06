using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.Notification;

public interface IRepositoryEventCollector
{
    void Add(string requestId, RepositoryEventType repositoryEventType, int resourceStoreId);
    void Add(RepositoryEvent repositoryEvent);
    
    public IReadOnlyCollection<RepositoryEvent> RepositoryEventList { get; }
    
    public void Clear();
}