using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.Notification;

public record RepositoryEvent(
    FhirResourceTypeId ResourceType,
    string RequestId,
    RepositoryEventType RepositoryEventType, 
    string ResourceId, 
    Domain.Configuration.Tenant Tenant,
    DateTime EventTimestampUtc) : 
    NotifyEventBase(EventTimestampUtc: EventTimestampUtc);