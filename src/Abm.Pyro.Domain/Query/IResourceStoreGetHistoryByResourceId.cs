using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreGetHistoryByResourceId
{
  Task<ResourceStoreSearchOutcome> Get(FhirResourceTypeId resourceType, string resourceId, SearchQueryServiceOutcome searchQueryServiceOutcome);
}
