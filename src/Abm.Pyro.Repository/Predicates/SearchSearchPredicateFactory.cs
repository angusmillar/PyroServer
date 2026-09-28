using LinqKit;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Predicates;

public class SearchSearchPredicateFactory(IResourceStorePredicateFactory resourceStorePredicateFactory) : ISearchPredicateFactory
{
  public ExpressionStarter<ResourceStore> CurrentMainResourcePredicate(FhirResourceTypeId resourceType)
  {
    return resourceStorePredicateFactory.CurrentMainResource(resourceType);
  }

  public async Task<ExpressionStarter<ResourceStore>> GetResourceStoreIndexPredicate(IEnumerable<SearchQueryBase> searchQueryList)
  {
    IEnumerable<SearchQueryBase> noChainedSearchQueryList = searchQueryList.Where(x => x.ChainedSearchParameter is null);

    ExpressionStarter<ResourceStore> predicateOuter = PredicateBuilder.New<ResourceStore>(true);
    foreach (var searchQuery in noChainedSearchQueryList)
    {
      ExpressionStarter<ResourceStore> predicateInner = PredicateBuilder.New<ResourceStore>(true);
      switch (searchQuery.SearchParameter.Type)
      {
        case SearchParamType.Number:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.NumberIndex(searchQuery));
          break;
        case SearchParamType.Date:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.DateTimeIndex(searchQuery));
          break;
        case SearchParamType.String:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.StringIndex(searchQuery));
          break;
        case SearchParamType.Token:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.TokenIndex(searchQuery));
          break;
        case SearchParamType.Reference:
          predicateInner = predicateInner.And(await resourceStorePredicateFactory.ReferenceIndex(searchQuery));
          break;
        case SearchParamType.Composite:
          predicateInner = await resourceStorePredicateFactory.CompositeIndex(this, searchQuery);
          break;
        case SearchParamType.Quantity:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.QuantityIndex(searchQuery));
          break;
        case SearchParamType.Uri:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.UriIndex(searchQuery));
          break;
        case SearchParamType.Special:
          predicateInner = predicateInner.And(resourceStorePredicateFactory.PositionIndex(searchQuery));
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(searchQuery.SearchParameter.Type), searchQuery.SearchParameter.Type.GetCode(), nameof(SearchParamType));
      }
      predicateOuter = predicateOuter.Extend(predicateInner, PredicateOperator.And);
    }
    return predicateOuter;
  }
}
