#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class IndexString : IndexBase
{

  private IndexString(): base() { }

    
  public IndexString(int? indexStringId, int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore, string value )
    :base(resourceStoreId, resourceStore, searchParameterStoreId, searchParameterStore)
  {
    IndexStringId = indexStringId;
    Value = value;
  }

  public int? IndexStringId { get; set; }
  public string Value { get; set; }
}
