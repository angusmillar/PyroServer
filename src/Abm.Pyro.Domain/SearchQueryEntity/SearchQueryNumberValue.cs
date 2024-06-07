using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryNumberValue(bool isMissing, SearchComparatorId? prefix, int? precision, int? scale, decimal? value)
  : SearchQueryValuePrefixBase(isMissing, prefix)
{
  public int? Precision { get; set; } = precision;
  public int? Scale { get; set; } = scale;
  public decimal? Value { get; set; } = value;
}
