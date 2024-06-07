using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.Model;

public class SearchParameterStoreResourceTypeTarget : DbBase
{
  private SearchParameterStoreResourceTypeTarget() { }
  
  public SearchParameterStoreResourceTypeTarget(int? searchParameterStoreResourceTypeTargetId, int searchParameterStoreId, FhirResourceTypeId resourceType)
  {
    SearchParameterStoreResourceTypeTargetId = searchParameterStoreResourceTypeTargetId;
    SearchParameterStoreId = searchParameterStoreId;
    ResourceType = resourceType;
  }
  public int? SearchParameterStoreResourceTypeTargetId { get; set; }
  public SearchParameterStore? SearchParameterStore { get; set; }
  public int SearchParameterStoreId { get; set; }
  public FhirResourceTypeId ResourceType { get; set; }
}
