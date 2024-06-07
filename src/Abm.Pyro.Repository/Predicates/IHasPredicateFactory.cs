using LinqKit;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IHasPredicateFactory
{
  Task<ExpressionStarter<ResourceStore>> GetHasPredicate(IList<SearchQueryHas> searchQueryList);
}
