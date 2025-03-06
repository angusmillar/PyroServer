using Abm.Pyro.Domain.SearchQuery;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreGetHistory
{
  Task<ResourceStoreSearchOutcome> Get(SearchQueryServiceOutcome searchQueryServiceOutcome);
}
