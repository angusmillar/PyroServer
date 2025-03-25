using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.Notification;

public record RepositoryEvent(
    FhirResourceTypeId ResourceType,
    RepositoryEventType RepositoryEventType, 
    string ResourceId, 
    Configuration.Tenant Tenant,
    DateTime EventTimestampUtc) : 
    NotifyEventBase(EventTimestampUtc: EventTimestampUtc);