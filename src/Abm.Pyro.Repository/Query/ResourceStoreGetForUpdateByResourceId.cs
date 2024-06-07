using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Repository.Query;

public class ResourceStoreGetForUpdateByResourceId(PyroDbContext context) : IResourceStoreGetForUpdateByResourceId
{
    
    public async Task<ResourceStoreUpdateProjection?> Get(FhirResourceTypeId resourceType, string resourceId)
    {
        return await context.Set<ResourceStore>()
            .Where(x =>
                x.ResourceId == resourceId &
                x.ResourceType == resourceType &
                x.IsCurrent == true)
            .Select(s => new ResourceStoreUpdateProjection(
                s.ResourceStoreId, 
                s.VersionId, 
                s.IsCurrent,
                s.IsDeleted))
            .FirstOrDefaultAsync();
    }
}
