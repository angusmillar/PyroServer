#pragma warning disable CS8618
namespace Abm.Pyro.Domain.Model;

public class IndexDateTime : IndexBase
{
  private IndexDateTime(): base() { }
  
  public IndexDateTime(int? indexDateTimeId, int? resourceStoreId, ResourceStore? resourceStore, int? searchParameterStoreId, SearchParameterStore? searchParameterStore, DateTime? lowUtc, DateTime? highUtc)
    :base(resourceStoreId, resourceStore, searchParameterStoreId, searchParameterStore)
  {
    IndexDateTimeId = indexDateTimeId;
    LowUtc = lowUtc;
    HighUtc = highUtc;
  }

  public int? IndexDateTimeId { get; set; }
  
  public DateTime? LowUtc { get; set; }
  
  public DateTime? HighUtc { get; set; }
}
