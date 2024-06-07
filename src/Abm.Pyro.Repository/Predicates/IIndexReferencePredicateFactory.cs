using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexReferencePredicateFactory
{
  Task<List<Expression<Func<IndexReference, bool>>>> ReferenceIndex(SearchQueryReference searchQueryReference);
}
