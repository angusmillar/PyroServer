using Abm.Pyro.Domain.Notification;

namespace Abm.Pyro.Application.FhirSubscriptions;

public interface IFhirNotificationService
{
    Task ProcessEventList(RepositoryEventSet repositoryEventSet, CancellationToken cancellationToken);
    
}