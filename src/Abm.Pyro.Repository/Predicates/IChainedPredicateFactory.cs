using LinqKit;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IChainedPredicateFactory
{
  Task<ExpressionStarter<ResourceStore>> GetChainedPredicate(IList<SearchQueryBase> searchQueryList);
}
