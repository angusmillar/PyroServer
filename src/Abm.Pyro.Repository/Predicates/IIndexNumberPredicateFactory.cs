using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexNumberPredicateFactory
{
  List<Expression<Func<IndexQuantity, bool>>> NumberIndex(SearchQueryNumber searchQueryNumber);
}
