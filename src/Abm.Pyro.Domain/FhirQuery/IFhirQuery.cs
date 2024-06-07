using Abm.Pyro.Domain.Enums;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirQuery;

public interface IFhirQuery
{
  ContainedSearch? Contained { get; }
  ContainedType? ContainedType { get; }
  string? Content { get; }
  int? Page { get; set; }
  int? Count { get; }
  string? Filter { get; }
  IList<FhirQuery.IncludeParameter> Include { get; }
  IList<InvalidQueryParameter> InvalidParameterList { get; }
  Dictionary<string, StringValues> ParametersDictionary { get; }
  string? Query { get; }
  IList<FhirQuery.SortParameter> Sort { get; }
  IList<FhirQuery.HasParameter> Has { get; set; }
  SummaryType? SummaryType { get; }
  string? Text { get; }
  bool Parse(Dictionary<string, StringValues> query);
}
