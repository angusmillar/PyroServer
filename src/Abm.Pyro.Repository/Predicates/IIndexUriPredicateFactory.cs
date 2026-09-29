using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexUriPredicateFactory
{
  Expression<Func<ResourceStore, bool>> UriIndex(SearchQueryUri searchQueryUri);
}
