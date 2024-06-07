using LinqKit;
using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.SearchQuery;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Repository.Predicates;
using Abm.Pyro.Repository.Service;
using Abm.Pyro.Repository.Extensions;

namespace Abm.Pyro.Repository.Query;

public class ResourceStoreSearch(
    PyroDbContext context,
    ISearchPredicateFactory searchPredicateFactory,
    IChainedPredicateFactory chainedPredicateFactory,
    IPaginationSupport paginationSupport,
    IHasPredicateFactory hasPredicateFactory,
    IResourceIncludesService resourceIncludesService)
    : IResourceStoreSearch
{
    public async Task<ResourceStoreSearchOutcome> GetSearch(SearchQueryServiceOutcome searchQueryServiceOutcome)
    {
        ExpressionStarter<ResourceStore> predicate = searchPredicateFactory.CurrentMainResourcePredicate(searchQueryServiceOutcome.ResourceContext);
        predicate.Extend(await searchPredicateFactory.GetResourceStoreIndexPredicate(searchQueryServiceOutcome.SearchQueryList), PredicateOperator.And);
        predicate.Extend(await chainedPredicateFactory.GetChainedPredicate(searchQueryServiceOutcome.SearchQueryList), PredicateOperator.And);
        predicate.Extend(await hasPredicateFactory.GetHasPredicate(searchQueryServiceOutcome.HasList), PredicateOperator.And);

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
            
            return new ResourceStoreSearchOutcome(
                searchTotal: totalRecordCount,
                pageRequested: pageRequired,
                pagesTotal: paginationSupport.CalculateTotalPages(searchQueryServiceOutcome.CountRequested, totalRecordCount),
                resourceStoreList: targetResourceStoreList,
                includedResourceStoreList: includedResourceStoreList);
        }
        catch (Exception exception)
        {
            string message = exception.Message;
            throw;
        }
    }
}