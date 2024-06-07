using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.SearchQuery;

namespace Abm.Pyro.Domain.Query;

public interface IResourceStoreGetHistoryByResourceType
{
  Task<ResourceStoreSearchOutcome> Get(FhirResourceTypeId resourceType, SearchQueryServiceOutcome searchQueryServiceOutcome);
}
