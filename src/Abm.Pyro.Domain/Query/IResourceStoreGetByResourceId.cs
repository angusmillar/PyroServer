using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreGetByResourceId
{
  Task<ResourceStore?> Get(string resourceId, FhirResourceTypeId resourceType);
}
