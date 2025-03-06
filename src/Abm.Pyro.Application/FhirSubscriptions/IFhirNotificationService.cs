using Abm.Pyro.Domain.Notification;

namespace Abm.Pyro.Application.FhirSubscriptions;

public interface IFhirNotificationService
{
    Task ProcessEventList(ICollection<RepositoryEvent> repositoryEventList, CancellationToken cancellationToken);
    
}