namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryStringValue(bool IsMissing, string? Value) : SearchQueryValueBase(IsMissing)
{
  public string? Value { get; set; } = Value;
}
