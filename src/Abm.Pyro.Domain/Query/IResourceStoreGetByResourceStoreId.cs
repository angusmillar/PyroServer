using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreGetByResourceStoreId
{
  Task<ResourceStore?> Get(int resourceStoreId);
}
