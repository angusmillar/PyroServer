using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Domain.SearchQueryChain;

public class ChainQueryProcessingOutcome
{
  public List<SearchQueryBase> SearchQueryList { get; } = new();
  public List<InvalidQueryParameter> InvalidSearchQueryList { get; } = new();
  public List<InvalidQueryParameter> UnsupportedSearchQueryList { get; } = new();
}
