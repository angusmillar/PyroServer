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

  /// <summary>
  /// Builds the ResourceStore-level predicate for a 'near' search, whether plain or ':missing'.
  /// Negation is expressed by the term's Negated flag rather than inside the index-row predicate,
  /// because an index-row predicate can only assert that a matching row exists.
  /// </summary>
  public Expression<Func<ResourceStore, bool>> PositionIndex(SearchQueryNear searchQueryNear)
  {
    if (!searchQueryNear.SearchParameter.SearchParameterStoreId.HasValue)
    {
      throw new ArgumentNullException(nameof(searchQueryNear.SearchParameter.SearchParameterStoreId));
    }

    int searchParameterId = searchQueryNear.SearchParameter.SearchParameterStoreId.Value;
    var terms = new List<IndexPredicateTerm<IndexPosition>>();

    if (searchQueryNear.Modifier.HasValue)
    {
      var arrayOfSupportedModifiers = FhirSearchQuerySupport.GetModifiersForSearchType(searchQueryNear.SearchParameter.Type);
      if (!arrayOfSupportedModifiers.Contains(searchQueryNear.Modifier.Value))
      {
        throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNear.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryNear.SearchParameter.Type.GetCode()}.");
      }

      if (searchQueryNear.Modifier.Value != SearchModifierCodeId.Missing)
      {
        throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryNear.Modifier.Value.GetCode()} has been added to the supported list for {searchQueryNear.SearchParameter.Type.GetCode()} search parameter queries and yet no database predicate has been provided.");
      }

      foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
      {
        // ':missing=true' asserts absence, ':missing=false' asserts presence. Folded with Or, so
        // ':missing=true,false' remains the tautology FHIR's comma-as-OR rule makes it.
        terms.Add(new IndexPredicateTerm<IndexPosition>(
          IsSearchParameterId(searchParameterId),
          Negated: nearValue.IsMissing));
      }
    }
    else
    {
      foreach (SearchQueryNearValue nearValue in searchQueryNear.ValueList)
      {
        terms.Add(new IndexPredicateTerm<IndexPosition>(
          AndAlso(IsSearchParameterId(searchParameterId), WithinDistanceOf(nearValue)),
          Negated: false));
      }
    }

    return IndexPredicateComposer.Compose(
      terms,
      PredicateCombine.Or,
      (predicate, negated) => negated
        ? x => !x.IndexPositionList.Any(predicate.Compile())
        : x => x.IndexPositionList.Any(predicate.Compile()));
  }

  /// <summary>
  /// Conjoins two index-row predicates. Named AndAlso rather than And because a private static
  /// 'And' shadows LinqKit's And extension method inside this class. The extension is invoked
  /// statically so no 'using LinqKit' is needed and no name resolution is ambiguous.
  /// </summary>
  private static Expression<Func<IndexPosition, bool>> AndAlso(
    Expression<Func<IndexPosition, bool>> left,
    Expression<Func<IndexPosition, bool>> right)
  {
    return LinqKit.PredicateBuilder.And(left, right);
  }

  /// <summary>
  /// Translates to STDistance(Position, @point) &lt;= @metres, which is the form SQL Server can
  /// serve from a spatial index. STDistance returns metres, and the search radius is already in
  /// metres by the time it reaches here.
  /// </summary>
  private static Expression<Func<IndexPosition, bool>> WithinDistanceOf(SearchQueryNearValue nearValue)
  {
    // NetTopologySuite orders a Point as (X, Y), which is (longitude, latitude).
    var targetPoint = new Point(nearValue.Longitude, nearValue.Latitude) { SRID = Wgs84Srid };
    double distanceInMetres = nearValue.DistanceInMetres;

    return x => x.Position.Distance(targetPoint) <= distanceInMetres;
  }

  private static Expression<Func<IndexPosition, bool>> IsSearchParameterId(int searchParameterId)
  {
    return x => x.SearchParameterStoreId == searchParameterId;
  }
}
