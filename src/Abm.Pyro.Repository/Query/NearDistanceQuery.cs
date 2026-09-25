using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQueryEntity;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Abm.Pyro.Repository.Query;

/// <summary>
/// Phase two of a Location 'near' search. The filtering predicate is a correlated EXISTS and so
/// cannot surface a computed distance; this runs afterwards over the single page of matched
/// resources and computes each one's distance with the same STDistance call that filtered it, so
/// the filter and the reported distance can never disagree at the radius boundary.
/// </summary>
public class NearDistanceQuery(PyroDbContext context) : INearDistanceQuery
{
  private const int Wgs84Srid = 4326;

  public async Task<IReadOnlyDictionary<int, NearDistance>> GetNearestDistances(
    IReadOnlyCollection<int> resourceStoreIdList,
    SearchQueryNear searchQueryNear)
  {
    var result = new Dictionary<int, NearDistance>();

    if (resourceStoreIdList.Count == 0 || !searchQueryNear.SearchParameter.SearchParameterStoreId.HasValue)
    {
      return result;
    }

    int searchParameterId = searchQueryNear.SearchParameter.SearchParameterStoreId.Value;

    // One query per searched position. A single query taking the minimum across N points would
    // need Math.Min inside the expression tree, which does not translate reliably to T-SQL, and
    // N is almost always one.
    foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
    {
      // A ':missing' term names no coordinate to measure from, so there is nothing to compute.
      if (nearValue.IsMissing)
      {
        continue;
      }

      // NetTopologySuite orders a Point as (X, Y), which is (longitude, latitude).
      var targetPoint = new Point(nearValue.Longitude, nearValue.Latitude) { SRID = Wgs84Srid };

      var distanceList = await context.Set<IndexPosition>()
        .Where(x => x.SearchParameterStoreId == searchParameterId &&
                    x.ResourceStoreId != null &&
                    resourceStoreIdList.Contains(x.ResourceStoreId.Value))
        .Select(x => new
        {
          ResourceStoreId = x.ResourceStoreId!.Value,
          Metres = x.Position.Distance(targetPoint)
        })
        .ToListAsync();

      foreach (var distance in distanceList)
      {
        if (!result.TryGetValue(distance.ResourceStoreId, out NearDistance? existing) ||
            distance.Metres < existing.Metres)
        {
          result[distance.ResourceStoreId] = new NearDistance(distance.Metres, nearValue.ReportUnit);
        }
      }
    }

    return result;
  }
}
