using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.Model;

public class SearchParameterStoreSearchModifierCode : DbBase
{
  private SearchParameterStoreSearchModifierCode() { }
  
  public SearchParameterStoreSearchModifierCode(int? searchParameterStoreSearchModifierCodeId, int searchParameterStoreId, SearchModifierCodeId? searchModifierCodeId)
  {
    SearchParameterStoreSearchModifierCodeId = searchParameterStoreSearchModifierCodeId;
    SearchModifierCodeId = searchModifierCodeId;
    SearchParameterStoreId = searchParameterStoreId;
  }
  public int? SearchParameterStoreSearchModifierCodeId { get; set; }
  
  public SearchParameterStore? SearchParameterStore { get; set; }
  public int SearchParameterStoreId { get; set; }
  public SearchModifierCodeId? SearchModifierCodeId { get; set; }

}
