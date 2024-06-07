using Abm.Pyro.Domain.SearchQuery;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreSearch
{
  Task<ResourceStoreSearchOutcome> GetSearch(SearchQueryServiceOutcome searchQueryServiceOutcome);
}
