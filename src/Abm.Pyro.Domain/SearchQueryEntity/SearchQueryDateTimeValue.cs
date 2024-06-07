using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Domain.SearchQueryEntity;

public class SearchQueryDateTimeValue(bool isMissing, SearchComparatorId? prefix, DateTimePrecision? precision, DateTime? value)
  : SearchQueryValuePrefixBase(isMissing, prefix)
{
  public DateTimePrecision? Precision { get; set; } = precision;
  public DateTime? Value { get; set; } = value;
}
