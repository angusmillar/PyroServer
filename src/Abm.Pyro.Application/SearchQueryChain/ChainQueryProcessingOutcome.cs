using Abm.Pyro.Domain.FhirQuery;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Application.SearchQueryChain;

public class ChainQueryProcessingOutcome
{
  public List<SearchQueryBase> SearchQueryList { get; set; } = new();
  public List<InvalidQueryParameter> InvalidSearchQueryList { get; set; } = new();
  public List<InvalidQueryParameter> UnsupportedSearchQueryList { get; set; } = new();
}
