using System.Linq.Expressions;
using LinqKit;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public class ChainedPredicateFactory(
  PyroDbContext context, 
  ISearchPredicateFactory searchPredicateFactory, 
  IServiceBaseUrlCache serviceBaseUrlCache,
  IOptions<IndexingSettings> indexingSettingsOptions)
  : IChainedPredicateFactory
{
  public async Task<ExpressionStarter<ResourceStore>> GetChainedPredicate(IList<SearchQueryBase> searchQueryList)
  {
    ServiceBaseUrl? primaryServiceBaseUrl = await serviceBaseUrlCache.GetPrimaryAsync();
    if (primaryServiceBaseUrl is null || primaryServiceBaseUrl.ServiceBaseUrlId is null)
    {
      throw new NullReferenceException(nameof(primaryServiceBaseUrl));
    }

    IEnumerable<SearchQueryBase> chainedSearchQueryList = searchQueryList.Where(x => x.ChainedSearchParameter is not null);

    ExpressionStarter<ResourceStore> predicate = PredicateBuilder.New<ResourceStore>(true);
    foreach (var chainedSearchQuery in chainedSearchQueryList)
    {
      if (chainedSearchQuery is SearchQueryReference searchQueryReference)
      {
        Expression<Func<IndexReference, bool>> chainedReferenceIndexQuery = await ChainedReference(searchQueryReference, primaryServiceBaseUrl.ServiceBaseUrlId.Value);
        predicate = predicate.And(p => p.IndexReferenceList.Any(chainedReferenceIndexQuery.Compile()));
      }
      else
      {
        throw new ApplicationException("All chained searchParameters must be of type SearchQueryReference");
      }
    }
    return predicate;
  }

  private async Task<Expression<Func<IndexReference, bool>>> ChainedReference(SearchQueryReference searchQueryReference, int primaryServiceBaseUrlId)
  {
    if (searchQueryReference.IsChained && searchQueryReference.ChainedSearchParameter is SearchQueryReference chainedSearchQueryReference && chainedSearchQueryReference.IsChained)
    {
      //Recursive call 
      Expression<Func<IndexReference, bool>> chainedReferencePredicate = await ChainedReference(chainedSearchQueryReference, primaryServiceBaseUrlId);
      
      var chainLinkReferenceIndexQuery = context.Set<ResourceStore>().AsExpandable().Where(x => x.IndexReferenceList.Any(chainedReferencePredicate.Compile()));

      return i =>
        i.SearchParameterStoreId == searchQueryReference.SearchParameter.SearchParameterStoreId &&
        i.ServiceBaseUrlId == primaryServiceBaseUrlId &&
        i.ResourceStore!.ResourceType == searchQueryReference.ResourceTypeContext &&
        chainLinkReferenceIndexQuery.Select(b => b.ResourceId).Contains(i.ResourceId);
    }

    ExpressionStarter<ResourceStore> finalNodePredicate = await searchPredicateFactory.GetResourceStoreIndexPredicate(new [] { searchQueryReference.ChainedSearchParameter! });
    IQueryable<ResourceStore> finalNodeQuery = context.Set<ResourceStore>().AsExpandable().Where(finalNodePredicate);

    if (indexingSettingsOptions.Value.RemoveHistoricResourceIndexesOnUpdateOrDelete)
    {
      return i =>
        i.SearchParameterStoreId == searchQueryReference.SearchParameter.SearchParameterStoreId &&
        i.ServiceBaseUrlId == primaryServiceBaseUrlId &&
        i.ResourceStore!.ResourceType == searchQueryReference.ResourceTypeContext &&
        finalNodeQuery.Select(b => b.ResourceId).Contains(i.ResourceId);
    }
    
    return i =>
        (i.SearchParameterStoreId == searchQueryReference.SearchParameter.SearchParameterStoreId &&
        i.ServiceBaseUrlId == primaryServiceBaseUrlId &&
        i.ResourceStore!.ResourceType == searchQueryReference.ResourceTypeContext &&
        i.VersionId != null &&
        finalNodeQuery.Select(b => b.ResourceId).Contains(i.ResourceId) &&
        finalNodeQuery.Select(b => b.VersionId.ToString()).Contains(i.VersionId))
      || 
        (i.SearchParameterStoreId == searchQueryReference.SearchParameter.SearchParameterStoreId &&
         i.ServiceBaseUrlId == primaryServiceBaseUrlId &&
         i.ResourceStore!.ResourceType == searchQueryReference.ResourceTypeContext &&
         i.VersionId == null &&
         finalNodeQuery.Select(b => b.ResourceId).Contains(i.ResourceId) &&
         finalNodeQuery.Select(b => b.IsCurrent).Contains(true) );
  }
}
