using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ResourceStoreUpdate(PyroDbContext context) : IResourceStoreUpdate
{
  public async Task Update(ResourceStoreUpdateProjection resourceStoreUpdateProjection, bool deleteFhirIndexes)
  {

    await context.Set<ResourceStore>()
      .Where(x => x.ResourceStoreId == resourceStoreUpdateProjection.ResourceStoreId)
      .ExecuteUpdateAsync(setters => setters
        .SetProperty(s => s.VersionId, resourceStoreUpdateProjection.VersionId)
        .SetProperty(s => s.IsCurrent, resourceStoreUpdateProjection.IsCurrent)
      );

    if (deleteFhirIndexes)
    {
      await context.Set<IndexString>()
        .Where(x => x.ResourceStoreId == resourceStoreUpdateProjection.ResourceStoreId)
        .ExecuteDeleteAsync();

      await context.Set<IndexReference>()
        .Where(x => x.ResourceStoreId == resourceStoreUpdateProjection.ResourceStoreId)
        .ExecuteDeleteAsync();

      await context.Set<IndexDateTime>()
        .Where(x => x.ResourceStoreId == resourceStoreUpdateProjection.ResourceStoreId)
        .ExecuteDeleteAsync();

      await context.Set<IndexQuantity>()
        .Where(x => x.ResourceStoreId == resourceStoreUpdateProjection.ResourceStoreId)
        .ExecuteDeleteAsync();

      await context.Set<IndexToken>()
        .Where(x => x.ResourceStoreId == resourceStoreUpdateProjection.ResourceStoreId)
        .ExecuteDeleteAsync();

      await context.Set<IndexUri>()
        .Where(x => x.ResourceStoreId == resourceStoreUpdateProjection.ResourceStoreId)
        .ExecuteDeleteAsync();
    }
  }
}
