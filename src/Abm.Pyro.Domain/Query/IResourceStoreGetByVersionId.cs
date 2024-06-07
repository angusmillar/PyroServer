using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreGetByVersionId
{
  Task<ResourceStore?> Get(string resourceId, int versionId, FhirResourceTypeId resourceType);
}
