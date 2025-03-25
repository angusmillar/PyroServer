namespace Abm.Pyro.Domain.Notification;

public record RepositoryEventSet(string RequestId, IReadOnlyCollection<RepositoryEvent> RepositoryEventList);