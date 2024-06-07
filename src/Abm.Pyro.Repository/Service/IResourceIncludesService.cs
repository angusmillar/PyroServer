using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Service;

public interface IResourceIncludesService
{
    Task<List<ResourceStore>> GetResourceIncludeList(List<ResourceStore> targetResourceStoreList, IList<SearchQueryInclude> searchQueryIncludeList);
}