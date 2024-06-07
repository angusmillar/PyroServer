using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Repository.Extensions;

namespace Abm.Pyro.Repository.Query;

public class ResourceStoreGetHistoryByResourceType(
  PyroDbContext context,
  IPaginationSupport paginationSupport) : IResourceStoreGetHistoryByResourceType
{
  public async Task<ResourceStoreSearchOutcome> Get(FhirResourceTypeId resourceType, SearchQueryServiceOutcome searchQueryServiceOutcome)
  {
    var query = context.Set<ResourceStore>()
      .Where(x =>
        x.ResourceType == resourceType);
    
    int totalRecordCount = await query.CountAsync();
    if (totalRecordCount == 0)
    {
      return ResourceStoreSearchOutcome.EmptyResult();
    }
    
    int pageRequired = paginationSupport.CalculatePageRequired(searchQueryServiceOutcome.PageRequested, searchQueryServiceOutcome.CountRequested, totalRecordCount);
    
    query = query.OrderByDescending(z => z.LastUpdatedUtc);
    query = query.Paging(pageRequired, paginationSupport.SetNumberOfRecordsPerPage(searchQueryServiceOutcome.CountRequested));
    
    List<ResourceStore> targetResourceStoreList =  await query.ToListAsync();
    
    return new ResourceStoreSearchOutcome(
      searchTotal: totalRecordCount, 
      pageRequested: pageRequired, 
      pagesTotal: paginationSupport.CalculateTotalPages(searchQueryServiceOutcome.CountRequested, totalRecordCount), 
      resourceStoreList: targetResourceStoreList,
      includedResourceStoreList: Enumerable.Empty<ResourceStore>().ToList());
  }
}
