using Abm.Pyro.Application.MetaDataService;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Cache;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Caching.Hybrid;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Cache;

public class MetaDataHybridCache(
  HybridCache hybridCache,
  ITenantService tenantService,
  IMetaDataService metaDataService)
  : IMetaDataCache
{
  
  const string CacheKey = "MetaData";
  const string CacheTag = "MetaData";
    
  public async Task<CapabilityStatement> GetCapabilityStatement()
  {
    return await hybridCache.GetOrCreateAsync<CapabilityStatement>(
      key: GetKey(), 
      factory: async _ => await metaDataService.GetCapabilityStatement(), 
      options: new HybridCacheEntryOptions(),
      tags: GetCacheTags(), 
      cancellationToken: CancellationToken.None);
  }
  
  public async Task Remove()
  {
    await hybridCache.RemoveAsync(GetKey());
  }
  
  private string GetKey()
  {
    return CacheSupport.GetCacheKey(tenantCode: tenantService.GetScopedTenant().Code, key: CacheKey);
  }
  
  private string[] GetCacheTags()
  {
    return [tenantService.GetScopedTenant().Code, CacheTag];
  }
  
}
