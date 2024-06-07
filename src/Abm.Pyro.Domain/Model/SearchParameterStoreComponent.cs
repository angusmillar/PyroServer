#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class SearchParameterStoreComponent : DbBase
{
  private SearchParameterStoreComponent() { }
  
  public SearchParameterStoreComponent(int? searchParameterStoreComponentId, int searchParameterStoreId, Uri definition, string expression)
  {
    SearchParameterStoreComponentId = searchParameterStoreComponentId;
    SearchParameterStoreId = searchParameterStoreId;
    Definition = definition;
    Expression = expression;
  }
  public int? SearchParameterStoreComponentId { get; set; }
  public SearchParameterStore? SearchParameterStore { get; set; }
  public int SearchParameterStoreId { get; set; }
  public Uri Definition { get; set; }
  public string Expression { get; set; }

}
