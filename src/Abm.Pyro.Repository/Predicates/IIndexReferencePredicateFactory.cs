using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexReferencePredicateFactory
{
  Task<Expression<Func<ResourceStore, bool>>> ReferenceIndex(SearchQueryReference searchQueryReference);
}
