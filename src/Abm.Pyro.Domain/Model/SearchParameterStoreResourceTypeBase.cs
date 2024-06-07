using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.Model;

public class SearchParameterStoreResourceTypeBase : DbBase
{
  private SearchParameterStoreResourceTypeBase() { }
  
  public SearchParameterStoreResourceTypeBase(int? searchParameterStoreResourceTypeBaseId, int searchParameterStoreId, FhirResourceTypeId resourceType)
  {
    SearchParameterStoreResourceTypeBaseId = searchParameterStoreResourceTypeBaseId;
    SearchParameterStoreId = searchParameterStoreId;
    ResourceType = resourceType;
  }
  public int? SearchParameterStoreResourceTypeBaseId { get; set; }
  public SearchParameterStore? SearchParameterStore { get; set; }
  public int SearchParameterStoreId { get; set; }
  public FhirResourceTypeId ResourceType { get; set; }
}
