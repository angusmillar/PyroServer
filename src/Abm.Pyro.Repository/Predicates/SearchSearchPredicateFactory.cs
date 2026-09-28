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
          resourceStorePredicateFactory.TokenIndex(searchQuery).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexTokenList.Any(x.Compile())));
          break;
        case SearchParamType.Reference:
          (await resourceStorePredicateFactory.ReferenceIndex(searchQuery)).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexReferenceList.Any(x.Compile())));
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
          if (searchQuery.Modifier == SearchModifierCodeId.Missing)
          {
            // ':missing' negates at the ResourceStore level, because IndexPosition holds rows for
            // exactly one search parameter and so an Any(...) over an empty list can never be true.
            predicateInner = predicateInner.And(resourceStorePredicateFactory.PositionIndexMissing(searchQuery));
            break;
          }

          resourceStorePredicateFactory.PositionIndex(searchQuery).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexPositionList.Any(x.Compile())));
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof(searchQuery.SearchParameter.Type), searchQuery.SearchParameter.Type.GetCode(), nameof(SearchParamType));
      }
      predicateOuter = predicateOuter.Extend(predicateInner, PredicateOperator.And);
    }
    return predicateOuter;
  }
}
