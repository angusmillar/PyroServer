using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public class ResourceStoreSearchOutcome(
  int searchTotal,
  int pageRequested,
  int pagesTotal,
  List<ResourceStore> resourceStoreList,
  List<ResourceStore> includedResourceStoreList)
{
  public int SearchTotal { get; } = searchTotal;
  public int PageRequested { get; } = pageRequested;
  public int PagesTotal { get; } = pagesTotal;
  public  List<ResourceStore> ResourceStoreList { get; } = resourceStoreList;
  public  List<ResourceStore> IncludedResourceStoreList { get; } = includedResourceStoreList;

  public static ResourceStoreSearchOutcome EmptyResult()
  {
    return new ResourceStoreSearchOutcome(
      searchTotal: 0, 
      pageRequested: 0, 
      pagesTotal: 0, 
      resourceStoreList: Enumerable.Empty<ResourceStore>().ToList(), 
      includedResourceStoreList: Enumerable.Empty<ResourceStore>().ToList());
  }
}
