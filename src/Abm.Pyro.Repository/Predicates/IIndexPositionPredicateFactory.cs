using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public interface IIndexPositionPredicateFactory
{
  /// <summary>
  /// Handles both the plain 'near' search and the ':missing' modifier. The latter used to need a
  /// separate method because the old index-row return type could not express negation.
  /// </summary>
  Expression<Func<ResourceStore, bool>> PositionIndex(SearchQueryNear searchQueryNear);
}
