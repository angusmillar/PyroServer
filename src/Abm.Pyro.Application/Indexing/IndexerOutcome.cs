using Abm.Pyro.Domain.Model;
namespace Abm.Pyro.Application.Indexing;

  public class IndexerOutcome(
    List<IndexString> stringIndexList,
    List<IndexReference> referenceIndexList,
    List<IndexDateTime> dateTimeIndexList,
    List<IndexQuantity> quantityIndexList,
    List<IndexToken> tokenIndexList,
    List<IndexUri> uriIndexList)
  {
    public List<IndexString> StringIndexList { get; private set; } = stringIndexList;
    public List<IndexReference> ReferenceIndexList { get; private set; } = referenceIndexList;
    public List<IndexDateTime> DateTimeIndexList { get; private set; } = dateTimeIndexList;
    public List<IndexQuantity> QuantityIndexList { get; private set; } = quantityIndexList;
    public List<IndexToken> TokenIndexList { get; private set; } = tokenIndexList;
    public List<IndexUri> UriIndexList { get; set; } = uriIndexList;
  }

