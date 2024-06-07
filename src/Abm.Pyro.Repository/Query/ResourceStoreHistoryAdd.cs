using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ResourceStoreHistoryAdd(PyroDbContext context) : IResourceStoreHistoryAdd
{
  public ResourceStore Add(ResourceStore resourceStore)
  {
    context.Add(resourceStore);
    return resourceStore;
  }
}
