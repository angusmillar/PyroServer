using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreGetHistory
{
  Task<ResourceStoreSearchOutcome> Get(SearchQueryServiceOutcome searchQueryServiceOutcome);
}
