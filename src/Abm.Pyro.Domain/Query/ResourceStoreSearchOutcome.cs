using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public class ResourceStoreSearchOutcome(
  int searchTotal,
  int pageRequested,
  int pagesTotal,
  List<ResourceStore> resourceStoreList,
  List<ResourceStore> includedResourceStoreList,
  IReadOnlyDictionary<int, NearDistance>? nearDistanceByResourceStoreId = null)
{
  public int SearchTotal { get; } = searchTotal;
  public int PageRequested { get; } = pageRequested;
  public int PagesTotal { get; } = pagesTotal;
  public  List<ResourceStore> ResourceStoreList { get; } = resourceStoreList;
  public  List<ResourceStore> IncludedResourceStoreList { get; } = includedResourceStoreList;

  /// <summary>
  /// For a Location 'near' search, each matched resource's distance from the nearest searched
  /// point. Null when the search had no near term, or when
  /// LocationNearSettings.ReturnDistanceInSearchResults is false.
  /// </summary>
  public IReadOnlyDictionary<int, NearDistance>? NearDistanceByResourceStoreId { get; } = nearDistanceByResourceStoreId;

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
