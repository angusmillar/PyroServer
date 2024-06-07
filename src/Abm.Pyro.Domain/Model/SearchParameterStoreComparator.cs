using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.Model;

public class SearchParameterStoreComparator : DbBase
{
  private SearchParameterStoreComparator() { }
  
  public SearchParameterStoreComparator(int? searchParameterStoreComparatorId, int searchParameterStoreId, SearchComparatorId? searchComparatorId)
  {
    SearchParameterStoreComparatorId = searchParameterStoreComparatorId;
    SearchComparatorId = searchComparatorId;
    SearchParameterStoreId = searchParameterStoreId;
  }
  
  public int? SearchParameterStoreComparatorId { get; set; }
  public SearchParameterStore? SearchParameterStore { get; set; }
  public int SearchParameterStoreId { get; set; }
  public SearchComparatorId? SearchComparatorId { get; set; }
  
  
}
