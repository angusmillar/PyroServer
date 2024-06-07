using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ResourceStoreAdd(PyroDbContext context) : IResourceStoreAdd
{
  public async Task<ResourceStore> Add(ResourceStore resourceStore)
  {
    context.Add(resourceStore);
    await context.SaveChangesAsync();
    return resourceStore;
  }
}
