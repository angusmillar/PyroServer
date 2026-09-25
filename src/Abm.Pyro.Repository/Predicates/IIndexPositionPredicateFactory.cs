using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public interface IIndexPositionPredicateFactory
{
  List<Expression<Func<IndexPosition, bool>>> PositionIndex(SearchQueryNear searchQueryNear);

  /// <summary>
  /// The ':missing' modifier, which must be expressed at the ResourceStore level rather than as
  /// an index-row predicate. See the note in the implementation for why.
  /// </summary>
  Expression<Func<ResourceStore, bool>> PositionIndexMissing(SearchQueryNear searchQueryNear);
}
