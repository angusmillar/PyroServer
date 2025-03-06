using Abm.Pyro.Domain.Notification;

namespace Abm.Pyro.Application.Notification;

public interface IRepositoryEventChannel
{
    Task AddAsync(ICollection<RepositoryEvent> repositoryEventList);
    Task ProcessAsync(CancellationToken cancellationToken);
}