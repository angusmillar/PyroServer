using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexStringPredicateFactory
{
  List<Expression<Func<IndexString, bool>>> StringIndex(SearchQueryString searchQueryString);
}
