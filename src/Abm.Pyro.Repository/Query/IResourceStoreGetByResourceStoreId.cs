using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Repository.Query;

public class ResourceStoreGetByResourceStoreId(PyroDbContext context) : IResourceStoreGetByResourceStoreId
{
  public async Task<ResourceStore?> Get(
      int resourceStoreId)
  {
      return await context.Set<ResourceStore>()
          .SingleOrDefaultAsync(x =>
              x.ResourceStoreId == resourceStoreId);
  }
}
