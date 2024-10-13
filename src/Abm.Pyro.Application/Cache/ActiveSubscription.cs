using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.Cache;

public record ActiveSubscription(
    int ResourceStoreId, 
    string ResourceId, 
    int VersionId, 
    FhirResourceTypeId CriteriaResourceType,
    string CriteriaQuery, 
    Uri Endpoint,
    string Payload, 
    string[] Headers,
    DateTimeOffset? EndDateTime);