using LinqKit;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
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
          resourceStorePredicateFactory.NumberIndex(searchQuery).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexQuantityList.Any(x.Compile())));
          break;
        case SearchParamType.Date:
          resourceStorePredicateFactory.DateTimeIndex(searchQuery).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexDateTimeList.Any(x.Compile())));
          break;
        case SearchParamType.String:
          resourceStorePredicateFactory.StringIndex(searchQuery).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexStringList.Any(x.Compile())));
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
          resourceStorePredicateFactory.QuantityIndex(searchQuery).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexQuantityList.Any(x.Compile())));
          break;
        case SearchParamType.Uri:
          resourceStorePredicateFactory.UriIndex(searchQuery).ForEach(x => predicateInner = predicateInner.Or(y => y.IndexUriList.Any(x.Compile())));
          break;
        case SearchParamType.Special:
          throw new FhirFatalException(System.Net.HttpStatusCode.InternalServerError, new string[] { $"Attempt to search with a SearchParameter of type: {SearchParamType.Special.GetCode()} which is not supported by this server." });
        default:
          throw new ArgumentOutOfRangeException(nameof(searchQuery.SearchParameter.Type), searchQuery.SearchParameter.Type.GetCode(), nameof(SearchParamType));
      }
      predicateOuter = predicateOuter.Extend(predicateInner, PredicateOperator.And);
    }
    return predicateOuter;
  }
}
