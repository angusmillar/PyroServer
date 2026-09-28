using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexStringPredicateFactory
{
  Expression<Func<ResourceStore, bool>> StringIndex(SearchQueryString searchQueryString);
}
