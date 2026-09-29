using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexTokenPredicateFactory
{
  Expression<Func<ResourceStore, bool>> TokenIndex(SearchQueryToken searchQueryToken);
}
