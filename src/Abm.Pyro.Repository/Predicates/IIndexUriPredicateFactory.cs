using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexUriPredicateFactory
{
  List<Expression<Func<IndexUri, bool>>> UriIndex(SearchQueryUri searchQueryUri);
}
