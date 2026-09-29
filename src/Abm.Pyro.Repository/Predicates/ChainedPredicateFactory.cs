using System.Linq.Expressions;
using LinqKit;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
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
    ExpressionStarter<ResourceStore> predicate = PredicateBuilder.New<ResourceStore>(true);

    List<SearchQueryBase> chainedSearchQueryList = searchQueryList.Where(x => x.ChainedSearchParameter is not null).ToList();
    if (chainedSearchQueryList.Count == 0)
    {
      return predicate;
    }

    ServiceBaseUrl primaryServiceBaseUrl = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
    if (primaryServiceBaseUrl.ServiceBaseUrlId is null)
    {
      throw new InvalidOperationException($"The primary {nameof(ServiceBaseUrl)} must have a {nameof(ServiceBaseUrl.ServiceBaseUrlId)}");
    }

    foreach (var chainedSearchQuery in chainedSearchQueryList)
    {
      if (chainedSearchQuery is not SearchQueryReference searchQueryReference)
      {
        throw new InvalidOperationException("All chained searchParameters must be of type SearchQueryReference");
      }

      Expression<Func<IndexReference, bool>> chainedReferenceIndexPredicate = await GetChainedReferencePredicate(searchQueryReference, primaryServiceBaseUrl.ServiceBaseUrlId.Value);
      // .Compile() is a LinqKit marker expanded by AsExpandable() at query time; it is never executed in-memory.
      predicate = predicate.And(p => p.IndexReferenceList.Any(chainedReferenceIndexPredicate.Compile()));
    }
    return predicate;
  }

  private async Task<Expression<Func<IndexReference, bool>>> GetChainedReferencePredicate(SearchQueryReference searchQueryReference, int primaryServiceBaseUrlId)
  {
    IQueryable<ResourceStore> chainTargetQuery = await GetChainTargetQuery(searchQueryReference, primaryServiceBaseUrlId);

    if (indexingSettingsOptions.Value.RemoveHistoricResourceIndexesOnUpdateOrDelete)
    {
      // Historic index rows are removed on update/delete, so any index row that matched a POSITIVE
      // chained criterion already belongs to a current resource version. That does not hold for a
      // negated terminal parameter such as ':missing' — see GetChainTargetQuery, which constrains
      // the target set to current, undeleted rows of the target type for exactly that reason.
      return i =>
        i.SearchParameterStoreId == searchQueryReference.SearchParameter.SearchParameterStoreId &&
        i.ServiceBaseUrlId == primaryServiceBaseUrlId &&
        i.ResourceStore!.ResourceType == searchQueryReference.ResourceTypeContext &&
        chainTargetQuery.Any(t => t.ResourceId == i.ResourceId);
    }

    // Historic index rows are retained, so the chain target set can contain historic resource
    // versions. The reference must resolve to the exact resource version that matched:
    // a version-specific reference (VersionId != null) must match both ResourceId and VersionId
    // on the SAME target row, and a non-versioned reference resolves to the current version.
    return i =>
      i.SearchParameterStoreId == searchQueryReference.SearchParameter.SearchParameterStoreId &&
      i.ServiceBaseUrlId == primaryServiceBaseUrlId &&
      i.ResourceStore!.ResourceType == searchQueryReference.ResourceTypeContext &&
      ((i.VersionId != null && chainTargetQuery.Any(t => t.ResourceId == i.ResourceId && t.VersionId.ToString() == i.VersionId)) ||
       (i.VersionId == null && chainTargetQuery.Any(t => t.ResourceId == i.ResourceId && t.IsCurrent)));
  }

  private async Task<IQueryable<ResourceStore>> GetChainTargetQuery(SearchQueryReference searchQueryReference, int primaryServiceBaseUrlId)
  {
    if (searchQueryReference.ChainedSearchParameter is SearchQueryReference chainedSearchQueryReference && chainedSearchQueryReference.IsChained)
    {
      // Intermediate chain link: the target set is the resources whose reference indexes
      // satisfy the next link of the chain (recursive).
      Expression<Func<IndexReference, bool>> nextLinkPredicate = await GetChainedReferencePredicate(chainedSearchQueryReference, primaryServiceBaseUrlId);
      return context.Set<ResourceStore>().AsExpandable().Where(x => x.IndexReferenceList.Any(nextLinkPredicate.Compile()));
    }

    // Final chain link: the target set is the resources matching the terminal search parameter,
    // restricted to current, undeleted resources of the chain's target type.
    //
    // All three constraints are load-bearing once the terminal parameter can be a NEGATED one such
    // as ':missing'. A positive terminal predicate is an EXISTS over index rows, and a historic or
    // deleted ResourceStore row has none (RemoveHistoricResourceIndexesOnUpdateOrDelete strips
    // them), so such rows could never satisfy it and the constraints were redundant. A NOT EXISTS
    // inverts precisely that: absence of index rows is what a historic row IS, so without
    // IsCurrent/IsDeleted every superseded and deleted version of the target would match. The
    // ResourceType constraint closes the matching third case, because GetChainedReferencePredicate
    // correlates on ResourceId alone and client-assigned ids make cross-type collisions reachable.
    SearchQueryBase terminalSearchParameter = searchQueryReference.ChainedSearchParameter!;
    FhirResourceTypeId targetResourceType = terminalSearchParameter.ResourceTypeContext;

    ExpressionStarter<ResourceStore> finalNodePredicate = await searchPredicateFactory.GetResourceStoreIndexPredicate([terminalSearchParameter]);
    finalNodePredicate = finalNodePredicate.And(x => x.IsCurrent && !x.IsDeleted && x.ResourceType == targetResourceType);

    return context.Set<ResourceStore>().AsExpandable().Where(finalNodePredicate);
  }
}
