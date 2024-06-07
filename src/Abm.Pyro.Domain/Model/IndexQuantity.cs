using Abm.Pyro.Domain.Enums;

#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class IndexQuantity : IndexBase
{
  private IndexQuantity(): base()
  {
  }
  
  public IndexQuantity(int? indexQuantityId, int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore, 
                       QuantityComparator? comparator, decimal? quantity, string? code, string? system, string? unit, 
                       QuantityComparator? comparatorHigh, decimal? quantityHigh, string? codeHigh, string? systemHigh, string? unitHigh)
    :base(resourceStoreId, resourceStore, searchParameterStoreId, searchParameterStore)
  {
    IndexQuantityId = indexQuantityId;
    Comparator = comparator;
    Quantity = quantity;
    Code = code;
    System = system;
    Unit = unit;
    ComparatorHigh = comparatorHigh;
    QuantityHigh = quantityHigh;
    CodeHigh = codeHigh;
    SystemHigh = systemHigh;
    UnitHigh = unitHigh;

  }

  public int? IndexQuantityId { get; set; }
  
  public QuantityComparator? Comparator { get; set; }
  public decimal? Quantity { get; set; }
  public string? Code { get; set; }
  public string? System { get; set; }
  public string? Unit { get; set; }

  public QuantityComparator? ComparatorHigh { get; set; }
  public decimal? QuantityHigh { get; set; }
  public string? CodeHigh { get; set; }
  public string? SystemHigh { get; set; }
  public string? UnitHigh { get; set; }
}
