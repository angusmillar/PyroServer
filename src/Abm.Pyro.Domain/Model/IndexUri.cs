#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class IndexUri : IndexBase
{
  private IndexUri(): base() { }
  
  public IndexUri(int? indexUriId, int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore, string uri )
    :base(resourceStoreId, resourceStore, searchParameterStoreId, searchParameterStore)
  {
    IndexUriId = indexUriId;
    Uri = uri;
  }

  public int? IndexUriId { get; set; }
  public string Uri { get; set; }
}
