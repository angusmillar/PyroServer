using Abm.Pyro.Application.Notification;

namespace Abm.Pyro.Application.FhirSubscriptions;

public interface IFhirNotificationService
{
    Task ProcessEventList(ICollection<RepositoryEvent> repositoryEventList);
}