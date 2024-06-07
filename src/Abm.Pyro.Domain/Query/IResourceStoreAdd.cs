using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreAdd
{
  public Task<ResourceStore> Add(ResourceStore resourceStore);
}
