using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreGetForUpdateByResourceId
{
  Task<ResourceStoreUpdateProjection?> Get(FhirResourceTypeId resourceType, string resourceId);
}
