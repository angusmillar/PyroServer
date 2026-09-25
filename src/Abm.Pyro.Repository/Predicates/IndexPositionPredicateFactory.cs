using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
using NetTopologySuite.Geometries;

namespace Abm.Pyro.Repository.Predicates;

public class IndexPositionPredicateFactory : IIndexPositionPredicateFactory
{
  private const int Wgs84Srid = 4326;

  public List<Expression<Func<IndexPosition, bool>>> PositionIndex(SearchQueryNear searchQueryNear)
  {
    var resultList = new List<Expression<Func<IndexPosition, bool>>>();

    foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
    {
      if (!searchQueryNear.SearchParameter.SearchParameterStoreId.HasValue)
      {
        throw new ArgumentNullException(nameof(searchQueryNear.SearchParameter.SearchParameterStoreId));
      }

      int searchParameterId = searchQueryNear.SearchParameter.SearchParameterStoreId.Value;

      if (searchQueryNear.Modifier.HasValue)
      {
        throw new ApplicationException($"Internal Server Error: {nameof(PositionIndex)} was called for a search query carrying the " +
                                       $"'{searchQueryNear.Modifier.Value.GetCode()}' modifier. Modified near queries are built by {nameof(PositionIndexMissing)}.");
      }

      var predicate = LinqKit.PredicateBuilder.New<IndexPosition>(true);
      predicate = predicate.And(IsSearchParameterId(searchParameterId));
      predicate = predicate.And(WithinDistanceOf(nearValue));
      resultList.Add(predicate);
    }

    return resultList;
  }

  /// <summary>
  /// Builds the ':missing' predicate at the ResourceStore level. It cannot be expressed as an
  /// index-row predicate inside IndexPositionList.Any(...) the way the sibling factories do,
  /// because IndexPosition holds rows for exactly one search parameter: the list is empty for
  /// every Location without a position, so any Any(...) over it is false and both
  /// ':missing=true' and ':missing=false' would match nothing.
  /// </summary>
  public Expression<Func<ResourceStore, bool>> PositionIndexMissing(SearchQueryNear searchQueryNear)
  {
    if (!searchQueryNear.SearchParameter.SearchParameterStoreId.HasValue)
    {
      throw new ArgumentNullException(nameof(searchQueryNear.SearchParameter.SearchParameterStoreId));
    }

    var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryNear.SearchParameter.Type);
    if (!searchQueryNear.Modifier.HasValue || !arrayOfSupportedModifiers.Contains(searchQueryNear.Modifier.Value))
    {
      throw new ApplicationException($"Internal Server Error: {nameof(PositionIndexMissing)} was called for a search query without a supported modifier.");
    }

    if (searchQueryNear.Modifier.Value != SearchModifierCodeId.Missing)
    {
      throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNear.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryNear.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
    }

    int searchParameterId = searchQueryNear.SearchParameter.SearchParameterStoreId.Value;

    var predicate = LinqKit.PredicateBuilder.New<ResourceStore>(true);

    foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
    {
      if (nearValue.IsMissing)
      {
        predicate = predicate.Or(y => !y.IndexPositionList.Any(i => i.SearchParameterStoreId == searchParameterId));
      }
      else
      {
        predicate = predicate.Or(y => y.IndexPositionList.Any(i => i.SearchParameterStoreId == searchParameterId));
      }
    }

    return predicate;
  }

  /// <summary>
  /// Translates to STDistance(Position, @point) &lt;= @metres, which is the form SQL Server can
  /// serve from a spatial index. STDistance returns metres, and the search radius is already in
  /// metres by the time it reaches here.
  /// </summary>
  private Expression<Func<IndexPosition, bool>> WithinDistanceOf(SearchQueryNearValue nearValue)
  {
    // NetTopologySuite orders a Point as (X, Y), which is (longitude, latitude).
    var targetPoint = new Point(nearValue.Longitude, nearValue.Latitude) { SRID = Wgs84Srid };
    double distanceInMetres = nearValue.DistanceInMetres;

    return x => x.Position.Distance(targetPoint) <= distanceInMetres;
  }

  private Expression<Func<IndexPosition, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }
}
