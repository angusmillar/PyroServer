#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class IndexToken : IndexBase
{

  private IndexToken(): base() {
    
  }
  
  public IndexToken(int? indexTokenId, int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore, 
                    string? code, string? system)
    :base(resourceStoreId, resourceStore, searchParameterStoreId, searchParameterStore)
  {
    IndexTokenId = indexTokenId;
    Code = code;
    System = system;
  }

  public int? IndexTokenId { get; set; }
  public string? Code { get; set; }
  public string? System { get; set; }
}
