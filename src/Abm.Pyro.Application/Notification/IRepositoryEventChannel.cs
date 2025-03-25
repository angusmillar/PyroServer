using Abm.Pyro.Domain.Notification;

namespace Abm.Pyro.Application.Notification;

public interface IRepositoryEventChannel
{
    Task AddAsync(RepositoryEventSet repositoryEventSet);
    Task ProcessAsync(CancellationToken cancellationToken);
}