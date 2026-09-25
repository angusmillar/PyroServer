using LinqKit;
using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.SearchQueryEntity;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Repository.Predicates;
using Abm.Pyro.Repository.Service;
using Abm.Pyro.Repository.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abm.Pyro.Repository.Query;

public class ResourceStoreSearch(
    ILogger<ResourceStoreSearch> logger,
    PyroDbContext context,
    ISearchPredicateFactory searchPredicateFactory,
    IChainedPredicateFactory chainedPredicateFactory,
    IPaginationSupport paginationSupport,
    IHasPredicateFactory hasPredicateFactory,
    IResourceIncludesService resourceIncludesService,
    INearDistanceQuery nearDistanceQuery,
    IOptions<LocationNearSettings> locationNearSettingsOptions)
    : IResourceStoreSearch
{
    
    public async Task<int> GetSearchTotalCount(SearchQueryServiceOutcome searchQueryServiceOutcome)
    {
        var predicate = await GetSearchPredicate(searchQueryServiceOutcome);
        try
        {
            IQueryable<ResourceStore> query = context.Set<ResourceStore>();
            query = query.AsExpandable().Where(predicate);
            return await query.CountAsync();
            
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error in GetSearchTotalCount");
            throw;
        }
    }
    
    public async Task<ResourceStoreSearchOutcome> GetSearch(SearchQueryServiceOutcome searchQueryServiceOutcome)
    {
        var predicate = await GetSearchPredicate(searchQueryServiceOutcome);

        try
        {
            IQueryable<ResourceStore> query = context.Set<ResourceStore>();
            query = query.AsExpandable().Where(predicate);

            int totalRecordCount = await query.CountAsync();
            if (totalRecordCount == 0)
            {
                return ResourceStoreSearchOutcome.EmptyResult();
            }

            int pageRequired = paginationSupport.CalculatePageRequired(searchQueryServiceOutcome.PageRequested, searchQueryServiceOutcome.CountRequested, totalRecordCount);

            query = query.OrderByDescending(z => z.LastUpdatedUtc);
            query = query.Paging(pageRequired, paginationSupport.SetNumberOfRecordsPerPage(searchQueryServiceOutcome.CountRequested));

            List<ResourceStore> targetResourceStoreList = await query.ToListAsync();
            
            List<ResourceStore> includedResourceStoreList = await resourceIncludesService.GetResourceIncludeList(targetResourceStoreList, searchQueryServiceOutcome.IncludeList);

            IReadOnlyDictionary<int, NearDistance>? nearDistanceByResourceStoreId =
                await GetNearDistancesIfRequired(searchQueryServiceOutcome, targetResourceStoreList);

            return new ResourceStoreSearchOutcome(
                searchTotal: totalRecordCount,
                pageRequested: pageRequired,
                pagesTotal: paginationSupport.CalculateTotalPages(searchQueryServiceOutcome.CountRequested, totalRecordCount),
                resourceStoreList: targetResourceStoreList,
                includedResourceStoreList: includedResourceStoreList,
                nearDistanceByResourceStoreId: nearDistanceByResourceStoreId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error in GetSearch");
            throw;
        }
    }

    private async Task<ExpressionStarter<ResourceStore>> GetSearchPredicate(SearchQueryServiceOutcome searchQueryServiceOutcome)
    {
        ExpressionStarter<ResourceStore> predicate = searchPredicateFactory.CurrentMainResourcePredicate(searchQueryServiceOutcome.ResourceContext);
        predicate.Extend(await searchPredicateFactory.GetResourceStoreIndexPredicate(searchQueryServiceOutcome.SearchQueryList), PredicateOperator.And);
        predicate.Extend(await chainedPredicateFactory.GetChainedPredicate(searchQueryServiceOutcome.SearchQueryList), PredicateOperator.And);
        predicate.Extend(await hasPredicateFactory.GetHasPredicate(searchQueryServiceOutcome.HasList), PredicateOperator.And);
        return predicate;
    }

    /// <summary>
    /// Computes each matched Location's distance from the searched point, but only when the
    /// server is configured to report it and the query actually contained a near term. This runs
    /// against the materialised page, never the whole result set.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, NearDistance>?> GetNearDistancesIfRequired(
        SearchQueryServiceOutcome searchQueryServiceOutcome,
        List<ResourceStore> targetResourceStoreList)
    {
        if (!locationNearSettingsOptions.Value.ReturnDistanceInSearchResults)
        {
            return null;
        }

        SearchQueryNear? searchQueryNear = searchQueryServiceOutcome.SearchQueryList
            .OfType<SearchQueryNear>()
            .FirstOrDefault(x => x.ChainedSearchParameter is null);

        if (searchQueryNear is null)
        {
            return null;
        }

        List<int> resourceStoreIdList = targetResourceStoreList
            .Where(x => x.ResourceStoreId.HasValue)
            .Select(x => x.ResourceStoreId!.Value)
            .ToList();

        return await nearDistanceQuery.GetNearestDistances(resourceStoreIdList, searchQueryNear);
    }
}