using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Query;

public interface INearDistanceQuery
{
  Task<IReadOnlyDictionary<int, NearDistance>> GetNearestDistances(
    IReadOnlyCollection<int> resourceStoreIdList,
    SearchQueryNear searchQueryNear);
}
