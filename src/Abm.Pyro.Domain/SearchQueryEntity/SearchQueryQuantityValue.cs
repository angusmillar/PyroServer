using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryQuantityValue(bool isMissing, SearchComparatorId? prefix, string? system, string? code, int? precision, int? scale, decimal? value)
  : SearchQueryValuePrefixBase(isMissing, prefix)
{
  public string? System { get; set; } = system;
  public string? Code { get; set; } = code;

  public int? Precision { get; set; } = precision;
  public int? Scale { get; set; } = scale;
  public decimal? Value { get; set; } = value;
}
