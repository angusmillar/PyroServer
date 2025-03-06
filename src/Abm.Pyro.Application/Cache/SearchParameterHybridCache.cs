using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;
using Microsoft.Extensions.Caching.Hybrid;

namespace Abm.Pyro.Application.Cache;

public class SearchParameterHybridCache(
  HybridCache hybridCache,
  ITenantService tenantService,
  ISearchParameterGetByBaseResourceType searchParameterGetByBaseResourceType)
  : ISearchParameterCache
{
  const string CacheClassCode = "SearchParams";
  
  public async Task<IEnumerable<SearchParameterProjection>> GetListByResourceType(FhirResourceTypeId resourceType)
  {
    return await hybridCache.GetOrCreateAsync<IEnumerable<SearchParameterProjection>>(
      key: GetKey(resourceType: resourceType), 
      factory: async _ => await searchParameterGetByBaseResourceType.Get(resourceType), 
      options: new HybridCacheEntryOptions(),
      tags: GetCacheTags());
  }

  public async Task Remove(FhirResourceTypeId resourceType)
  {
    await hybridCache.RemoveAsync(key: GetKey(resourceType));
  }

  private string GetKey(FhirResourceTypeId resourceType)
  {
    return CacheSupport.GetCacheKey(tenantCode: tenantService.GetScopedTenant().Code, key: $"{CacheClassCode}:{resourceType.GetCode()}");
  }
  
  private string[] GetCacheTags()
  {
    return [tenantService.GetScopedTenant().Code, CacheClassCode];
  }
  
}
