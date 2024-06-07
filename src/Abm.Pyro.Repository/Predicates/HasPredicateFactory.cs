using System.Linq.Expressions;
using LinqKit;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;
namespace Abm.Pyro.Repository.Predicates;

public class HasPredicateFactory(PyroDbContext context, ISearchPredicateFactory searchPredicateFactory, IServiceBaseUrlCache serviceBaseUrlCache)
  : IHasPredicateFactory
{
  public async Task<ExpressionStarter<ResourceStore>> GetHasPredicate(IList<SearchQueryHas> searchQueryHasList)
  {
    ServiceBaseUrl? primaryServiceBaseUrl = await serviceBaseUrlCache.GetPrimaryAsync();
    if (primaryServiceBaseUrl is null || primaryServiceBaseUrl.ServiceBaseUrlId is null)
    {
      throw new NullReferenceException(nameof(primaryServiceBaseUrl));
    }

    ExpressionStarter<ResourceStore> resourceStoreHasPredicate = PredicateBuilder.New<ResourceStore>(true);
    foreach (var searchQueryHas in searchQueryHasList)
    {
      Expression<Func<ResourceStore, bool>> resourceStoreTargetPredicate = await GetResourceStoreTargetPredicate(searchQueryHas, primaryServiceBaseUrl.ServiceBaseUrlId.Value);
      IQueryable<ResourceStore> resourceStoreTargetQuery = context.Set<ResourceStore>().AsExpandable().Where(resourceStoreTargetPredicate);
      
      resourceStoreHasPredicate = resourceStoreHasPredicate.And(resHas => 
                                                                  resourceStoreTargetQuery.Select(resTarget => 
                                                                                                    resTarget.ResourceStoreId).Contains(resHas.ResourceStoreId));
    }
    return resourceStoreHasPredicate;
  }

  private async Task<Expression<Func<ResourceStore, bool>>> GetResourceStoreTargetPredicate(SearchQueryHas searchQueryHas, int primaryServiceBaseUrlId)
  {
    Expression<Func<IndexReference, bool>> referenceIndexPredicate = await GetReferenceIndexPredicate(searchQueryHas, primaryServiceBaseUrlId);
    IQueryable<IndexReference> referenceIndexQuery = context.Set<IndexReference>().AsExpandable().Where(referenceIndexPredicate);

    return res => referenceIndexQuery.Select(rIx => rIx.ResourceId).Contains(res.ResourceId);
  }

  private async Task<Expression<Func<IndexReference, bool>>> GetReferenceIndexPredicate(SearchQueryHas searchQueryHas, int primaryServiceBaseUrlId)
  {
    
    
    
    //--------------------------------------------------------
    if (searchQueryHas.ChildSearchQueryHas is not null)
    {
      Expression<Func<IndexReference, bool>> chainedReferencePredicate = await GetReferenceIndexPredicate(searchQueryHas.ChildSearchQueryHas, primaryServiceBaseUrlId);
      
      var chainLinkReferenceIndexQuery = context.Set<ResourceStore>().AsExpandable().Where(x => x.IndexReferenceList.Any(chainedReferencePredicate.Compile()));

      return rIx =>
        rIx.SearchParameterStoreId == searchQueryHas.BackReferenceSearchParameter!.SearchParameterStoreId &&
        rIx.ServiceBaseUrlId == primaryServiceBaseUrlId &&
        rIx.ResourceStore!.ResourceType == searchQueryHas.TargetResourceForSearchQuery &&
        chainLinkReferenceIndexQuery.Select(b => b.ResourceId).Contains(rIx.ResourceId) &&
        chainLinkReferenceIndexQuery.Select(b => b.ResourceType).Contains(rIx.ResourceType);
    }
    //--------------------------------------------------------
    
    
    
    
    
    ExpressionStarter<ResourceStore> finalIndexNodePredicate = await searchPredicateFactory.GetResourceStoreIndexPredicate(new[] { searchQueryHas.SearchQuery! });
    IQueryable<ResourceStore> finalIndexNodeQuery = context.Set<ResourceStore>().AsExpandable().Where(finalIndexNodePredicate);
    
    return rIx =>
        rIx.SearchParameterStoreId == searchQueryHas.BackReferenceSearchParameter!.SearchParameterStoreId &&
        rIx.ServiceBaseUrlId == primaryServiceBaseUrlId &&
        rIx.ResourceStore!.ResourceType == searchQueryHas.TargetResourceForSearchQuery &&
        finalIndexNodeQuery.Select(res => res.ResourceType).Contains(searchQueryHas.TargetResourceForSearchQuery);
  }

}
