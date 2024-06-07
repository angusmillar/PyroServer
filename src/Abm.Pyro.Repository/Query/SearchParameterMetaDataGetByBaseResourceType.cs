using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class SearchParameterMetaDataGetByBaseResourceType(PyroDbContext context) : ISearchParameterMetaDataGetByBaseResourceType
{
  public async Task<IEnumerable<SearchParameterMetaDataProjection>> Get(FhirResourceTypeId resourceType)
  {
    return await context.Set<SearchParameterStore>()
                  .Include(x => x.BaseList)
                  .Include(x => x.TargetList)
                  .Where(x =>
                           x.Status == PublicationStatusId.Active &
                           x.IsCurrent == true &
                           x.IsDeleted == false &
                           x.IsIndexed == true &
                           x.BaseList.Any(o => 
                               o.ResourceType == resourceType))
                  .Select(p =>
                            new SearchParameterMetaDataProjection(
                              p.SearchParameterStoreId,
                              p.Code,
                              p.Url,
                              p.Type,
                              p.BaseList,
                              p.TargetList))
                  .AsSplitQuery()
                  .ToArrayAsync();
  }
}
