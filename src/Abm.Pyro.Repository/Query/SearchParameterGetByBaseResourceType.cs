using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class SearchParameterGetByBaseResourceType(PyroDbContext context) : ISearchParameterGetByBaseResourceType
{
  public async Task<IEnumerable<SearchParameterProjection>> Get(FhirResourceTypeId resourceType)
  {
    return await context.Set<SearchParameterStore>()
                  .Include(x => x.BaseList)
                  .Include(x => x.ComparatorList)
                  .Include(x => x.ComponentList)
                  .Include(x => x.ModifierList)
                  .Include(x => x.TargetList)
                  .Where(x =>
                           x.Status == PublicationStatusId.Active &
                           x.IsCurrent == true &
                           x.IsDeleted == false &
                           x.IsIndexed == true &
                           x.BaseList.Any(o => 
                               o.ResourceType == resourceType))
                  .Select(p =>
                            new SearchParameterProjection(
                              p.SearchParameterStoreId,
                              p.Code,
                              p.Status,
                              p.IsCurrent,
                              p.IsDeleted,
                              p.Url,
                              p.Type,
                              p.Expression,
                              p.MultipleOr,
                              p.MultipleAnd,
                              p.BaseList,
                              p.TargetList,
                              p.ComparatorList,
                              p.ModifierList,
                              p.ComponentList))
                  .AsSplitQuery()
                  .ToArrayAsync();
  }
}
