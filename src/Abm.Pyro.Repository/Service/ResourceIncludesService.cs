using System.Linq.Expressions;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.SearchQueryEntity;

namespace Abm.Pyro.Repository.Service;

public class ResourceIncludesService(
    PyroDbContext context,
    IOptions<IncludeRevIncludeSettings> includeRevIncludeSettings,
    IServiceBaseUrlCache serviceBaseUrlCache)
    : IResourceIncludesService
{
    private List<ResourceStore> FinalIncludedResourceStoreList = new();
    private HashSet<ResourceKeys> TargetResourceKeysHashSet = new();
    private readonly HashSet<ResourceKeys> ObtainedResourceKeyHashSet = new();
    private int IterationCount = 1;
    private int PrimaryServiceBaseUrlId;

    public async Task<List<ResourceStore>> GetResourceIncludeList(List<ResourceStore> targetResourceStoreList,
        IList<SearchQueryInclude> searchQueryIncludeList)
    {
        if (!searchQueryIncludeList.Any() || !targetResourceStoreList.Any())
        {
            return Enumerable.Empty<ResourceStore>().ToList();
        }

        PrimaryServiceBaseUrlId = await GetPrimaryServiceBaseUrlId();
        GetTargetResourceKeys(targetResourceStoreList);
        List<ResourceStore> currentIncludedResourceStoreList = await GetIncludeList(searchQueryIncludeList);
        FinalIncludedResourceStoreList = new List<ResourceStore>(currentIncludedResourceStoreList);
        ManageTargetResourceKeys(currentIncludedResourceStoreList);
        while (ContinueToIterate(currentIncludedResourceStoreList, searchQueryIncludeList))
        {
            currentIncludedResourceStoreList = await GetIncludeList(searchQueryIncludeList.Where(x => x.IsRecurseIterate).ToList());
            if (currentIncludedResourceStoreList.Any())
            {
                ManageTargetResourceKeys(currentIncludedResourceStoreList);
                FinalIncludedResourceStoreList.AddRange(currentIncludedResourceStoreList);
            }

            IterationCount++;
        }

        return FinalIncludedResourceStoreList;
    }

    private async Task<int> GetPrimaryServiceBaseUrlId()
    {
        ServiceBaseUrl primaryServiceBaseUrl = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
        if (!primaryServiceBaseUrl.ServiceBaseUrlId.HasValue)
        {
            throw new NullReferenceException(nameof(primaryServiceBaseUrl.ServiceBaseUrlId));
        }

        return primaryServiceBaseUrl.ServiceBaseUrlId.Value;
    }

    private void ManageTargetResourceKeys(List<ResourceStore> currentIncludedResourceStoreList)
    {
        ObtainedResourceKeyHashSet.UnionWith(TargetResourceKeysHashSet);
        GetTargetResourceKeys(currentIncludedResourceStoreList);
    }

    private void GetTargetResourceKeys(List<ResourceStore> currentIncludedResourceStoreList)
    {
        //Get the next set of Target Resource Key excluding the ones already obtained
        TargetResourceKeysHashSet = currentIncludedResourceStoreList.Where(x =>
                !ObtainedResourceKeyHashSet.Any(c =>
                    c.ResourceType.Equals(x.ResourceType) &&
                    c.ResourceId.Equals(x.ResourceId, StringComparison.Ordinal) &&
                    c.versionId.Equals(x.VersionId)))
            .Select(x => new ResourceKeys(x.ResourceStoreId!.Value, x.ResourceType, x.ResourceId, x.VersionId)).ToHashSet();
    }

    private bool ContinueToIterate(List<ResourceStore> currentIncludedResourceStoreList,
        IList<SearchQueryInclude> searchQueryIncludeList)
    {
        ThrowIfMaximumIterationsReached();
        ThrowIfMaximumNumberOfIncludeResourcesReached();
        return currentIncludedResourceStoreList.Any() && searchQueryIncludeList.Any(x => x.IsRecurseIterate);
    }

    private void ThrowIfMaximumNumberOfIncludeResourcesReached()
    {
        if (FinalIncludedResourceStoreList.Count > includeRevIncludeSettings.Value.MaximumIncludeResources)
        {
            throw new FhirErrorException(
                httpStatusCode: HttpStatusCode.PreconditionFailed,
                message: "The maximum number of _include or _revinclude resource additions has been reached. " +
                         "Please modify the search queries use of includes search parameters, " +
                         $"or review the servers {Abm.Pyro.Domain.Configuration.IncludeRevIncludeSettings.SectionName}.{nameof(includeRevIncludeSettings.Value.MaximumIncludeResources)} setting of: {includeRevIncludeSettings.Value.MaximumIncludeResources}");
        }
    }

    private void ThrowIfMaximumIterationsReached()
    {
        if (IterationCount > includeRevIncludeSettings.Value.MaximumIterations)
        {
            throw new FhirErrorException(
                httpStatusCode: HttpStatusCode.PreconditionFailed,
                message: "The maximum _include or _revinclude iterations has been reached. " +
                         "Please modify the search queries use of includes iterate search parameters, " +
                         $"or review the servers {Abm.Pyro.Domain.Configuration.IncludeRevIncludeSettings.SectionName}.{nameof(includeRevIncludeSettings.Value.MaximumIncludeResources)} setting of: {includeRevIncludeSettings.Value.MaximumIterations}");
        }
    }

    private async Task<List<ResourceStore>> GetIncludeList(IList<SearchQueryInclude> searchQueryIncludeList)
    {
        var includedResourceStoreList = new List<ResourceStore>();
        foreach (var searchQueryInclude in searchQueryIncludeList)
        {
            includedResourceStoreList.AddRange(await AppendInclude(searchQueryInclude));
        }

        return includedResourceStoreList;
    }

    private async Task<List<ResourceStore>> AppendInclude(SearchQueryInclude searchQueryInclude)
    {
        switch (searchQueryInclude.Type)
        {
            case IncludeType.Include:
                return await context.Set<ResourceStore>()
                    .Where(GetIncludeResourceStoreWhere(searchQueryInclude)).ToListAsync();
            case IncludeType.Revinclude:
                //return await AppendReverseInclude(searchQueryInclude);
                return await context.Set<ResourceStore>()
                    .Where(GetRevIncludeResourceStoreWhere(searchQueryInclude)).ToListAsync();
            default:
                throw new ArgumentOutOfRangeException(nameof(searchQueryInclude.Type));
        }
    }

    private Expression<Func<ResourceStore, bool>> GetIncludeResourceStoreWhere(SearchQueryInclude searchQueryInclude)
    {
        //Inner Select for the ResourceType and FhirResourceId from the IndexReference table
        var indexReferenceWhereClause = context.Set<IndexReference>()
            .Where(GetIndexReferenceWhereQuery(searchQueryInclude))
            .Select(x => new
            {
                x.ResourceType,
                x.ResourceId
            });
        
        return x =>
            indexReferenceWhereClause.Select(t => t.ResourceType).Contains(x.ResourceType) &&
            indexReferenceWhereClause.Select(t => t.ResourceId).Contains(x.ResourceId) &&
            x.IsCurrent &&
            !x.IsDeleted;
    }

    private Expression<Func<ResourceStore, bool>> GetRevIncludeResourceStoreWhere(SearchQueryInclude searchQueryInclude)
    {
        //Inner Select for the ResourceStoreId from the IndexReference table
        var indexReferenceWhereClause = context.Set<IndexReference>()
            .Where(GetRevIncludeIndexReferenceWhereQuery(searchQueryInclude))
            .Select(x => new
            {
                x.ResourceStoreId,
            });
        
        return x =>
            x.ResourceStoreId.HasValue &&
            x.ResourceType == searchQueryInclude.SourceResourceType &&
            indexReferenceWhereClause.Select(t => t.ResourceStoreId).Contains(x.ResourceStoreId) &&
            x.IsCurrent &&
            !x.IsDeleted;
    }

    private Expression<Func<IndexReference, bool>> GetIndexReferenceWhereQuery(SearchQueryInclude searchQueryInclude)
    {
        IEnumerable<int> targetResourceStoreIdList = TargetResourceKeysHashSet
            .Select(x => x.ResourceStoreId);
        
        return x =>
            (!searchQueryInclude.SearchParameterTargetResourceType.HasValue || x.ResourceType == searchQueryInclude.SearchParameterTargetResourceType) &&
            searchQueryInclude.SearchParameterList.Select(m => m.SearchParameterStoreId).Contains(x.SearchParameterStoreId) &&
            x.ResourceStoreId.HasValue &&
            x.ServiceBaseUrlId.HasValue &&
            x.ServiceBaseUrlId.Value == PrimaryServiceBaseUrlId &&
            targetResourceStoreIdList.Contains(x.ResourceStoreId.Value) &&
            x.ResourceStore!.ResourceType == searchQueryInclude.SourceResourceType;
    }

    private Expression<Func<IndexReference, bool>> GetRevIncludeIndexReferenceWhereQuery(SearchQueryInclude searchQueryInclude)
    {
        IEnumerable<string> targetResourceIdList = TargetResourceKeysHashSet
            .Where(x => x.ResourceType.Equals(searchQueryInclude.SearchParameterTargetResourceType))
            .Select(s => s.ResourceId);
        
        return x =>
            (!searchQueryInclude.SearchParameterTargetResourceType.HasValue || x.ResourceType == searchQueryInclude.SearchParameterTargetResourceType) &&
            searchQueryInclude.SearchParameterList.Select(m => m.SearchParameterStoreId).Contains(x.SearchParameterStoreId) &&
            x.ServiceBaseUrlId.HasValue &&
            x.ServiceBaseUrlId.Value == PrimaryServiceBaseUrlId &&
            targetResourceIdList.Contains(x.ResourceId) &&
            x.ResourceStore!.ResourceType == searchQueryInclude.SourceResourceType &&
            x.ResourceStore!.IsCurrent == true;
    }
    
    private class ResourceKeys(
        int resourceStoreId,
        FhirResourceTypeId resourceType,
        string resourceId,
        int? versionId)
    {
        public int ResourceStoreId { get; } = resourceStoreId;
        public FhirResourceTypeId ResourceType { get; } = resourceType;
        public string ResourceId { get; } = resourceId;
        public int? versionId { get; } = versionId;
    }
}

// ===== _includes Support ================================================
//https://hl7.org/fhir/R4/search.html#include
//e.g : GET [Base]/Patient?_id=pat1&_include=Patient:organization:Organization
//or
// e.g : GET [Base]/Patient?_id=pat1&_include=Patient:organization
// or 
// e.g : GET [Base]/Patient?_id=pat1&_include=Patient:*
// or
// e.g : GET [Base]/Patient?_id=pat1&_include=Patient:*:organization
// 
//Where: _include=1:2:3
// 1. The name of the source resource from which the join comes
// 2. The name of the search parameter which must be of type reference, can be * for any
// 3. (Optional) A specific of type of target resource (for when the search parameter refers to multiple possible target types)
//
// _include and _revinclude parameters do not include multiple values. Instead, the parameters are repeated for each different include criteria.
//Note that Version aware _include and _revinclude are not possible as a bundle of type 'Search' can not hold many instances of the same resource type.
//So you can't have Version 1 and Version 2 of the ame resource in a search bundle.
//See conversation here: https://chat.fhir.org/#narrow/stream/179166-implementers/topic/revinclude.3Aiterate.20and.20versioned.20references

// ===== _revincludes Support ================================================
//https://hl7.org/fhir/R4/search.html#revinclude
//The below is supported 
//e.g : GET [Base]/Organization?_id=Org1&_revinclude=Patient:organization:Organization
//or
// e.g : GET [Base]/Organization?_id=Org1&_revinclude=Patient:organization
// or 
// e.g : GET [Base]/Organization?_id=Org1&_revinclude=Patient:*
// or
// e.g : GET [Base]/Organization?_id=Org1&_revinclude=Patient:*:Organization
// 
//Where: _include=1:2:3
// 1. The name of the source resource from which the join comes
// 2. The name of the search parameter which must be of type reference, can be * for any
// 3. (Optional) A specific of type of target resource (for when the search parameter refers to multiple possible target types)
//
//Note that Version aware _include and _revinclude are not possible as a bundle of type 'Search' can not hold many instances of the same resource type.
//So you can't have Version 1 and Version 2 of the ame resource in a search bundle.
//See conversation here: https://chat.fhir.org/#narrow/stream/179166-implementers/topic/revinclude.3Aiterate.20and.20versioned.20references
