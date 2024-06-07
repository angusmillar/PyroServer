#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public abstract class IndexBase : DbBase
{
  protected IndexBase() : base() { }
  
  public IndexBase(int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore)
  {
    ResourceStoreId = resourceStoreId;
    ResourceStore = resourceStore;
    SearchParameterStoreId = searchParameterStoreId;
    SearchParameterStore = searchParameterStore;
  }
  
  public int? ResourceStoreId { get; set; }
  public ResourceStore? ResourceStore { get; set; }
  public int? SearchParameterStoreId { get; set; }
  public SearchParameterStore? SearchParameterStore { get; set; }
}
