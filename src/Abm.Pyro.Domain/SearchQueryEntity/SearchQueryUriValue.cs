namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryUriValue(bool IsMissing, Uri? Value) : SearchQueryValueBase(IsMissing)
{
  public Uri? Value { get; set; } = Value;
}
