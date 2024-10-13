using Abm.Pyro.Application.Cache;
using Abm.Pyro.Application.Notification;

namespace Abm.Pyro.Application.FhirSubscriptions;

public interface IFhirNotificationService
{
    Task ProcessEventList(ICollection<RepositoryEvent> repositoryEventList, CancellationToken cancellationToken);
    
}