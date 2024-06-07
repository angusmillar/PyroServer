using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexTokenPredicateFactory
{
  List<Expression<Func<IndexToken, bool>>> TokenIndex(SearchQueryToken searchQueryToken);
}
