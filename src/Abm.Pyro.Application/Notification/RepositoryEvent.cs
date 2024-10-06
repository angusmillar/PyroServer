using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.Notification;

public record RepositoryEvent(
    string RequestId,
    RepositoryEventType RepositoryEventType, 
    int ResourceStoreId, 
    Domain.Configuration.Tenant Tenant,
    DateTime EventTimestampUtc) : 
    NotifyEventBase(EventTimestampUtc: EventTimestampUtc);