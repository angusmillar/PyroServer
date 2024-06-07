using System.Linq.Expressions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public interface IIndexCompositePredicateFactory
{
  Task<Expression<Func<ResourceStore, bool>>> CompositeIndex(ISearchPredicateFactory searchPredicateFactory, SearchQueryComposite searchQueryComposite);
}
