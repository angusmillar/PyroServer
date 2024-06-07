using System.Linq.Expressions;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public class IndexCompositePredicateFactory : IIndexCompositePredicateFactory
{
  public async Task<Expression<Func<ResourceStore, bool>>> CompositeIndex(ISearchPredicateFactory searchPredicateFactory, SearchQueryComposite searchQueryComposite)
  {
    var resourceStorePredicate = LinqKit.PredicateBuilder.New<ResourceStore>(true);

    foreach (SearchQueryCompositeValue compositeValue in searchQueryComposite.ValueList)
    {
      if (searchQueryComposite.Modifier.HasValue)
      {
        throw new ApplicationException($"Internal Server Error: The search query modifier: {searchQueryComposite.Modifier.Value.GetCode()} is not supported for search parameter types of {searchQueryComposite.SearchParameter.Type.GetCode()}.");
      }
      if (compositeValue.SearchQueryBaseList is null)
      {
        throw new ArgumentNullException(nameof(compositeValue.SearchQueryBaseList));
      }
      resourceStorePredicate = resourceStorePredicate.Or(await searchPredicateFactory.GetResourceStoreIndexPredicate(compositeValue.SearchQueryBaseList));
    }
    return resourceStorePredicate;
  }
}
