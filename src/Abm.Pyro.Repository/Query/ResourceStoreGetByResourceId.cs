using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Repository.Query;

public class ResourceStoreGetByResourceId(PyroDbContext context) : IResourceStoreGetByResourceId
{
  public async Task<ResourceStore?> Get(string resourceId, FhirResourceTypeId resourceType)
  {
    return await context.Set<ResourceStore>()
                        .SingleOrDefaultAsync(x =>
                                                x.ResourceId == resourceId &
                                                x.ResourceType == resourceType &
                                                x.IsCurrent == true);
  }
}
