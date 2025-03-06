using Abm.Pyro.Application.FhirSubscriptions;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Cache;
using Microsoft.Extensions.Caching.Hybrid;

namespace Abm.Pyro.Application.Cache;

public class ActiveSubscriptionHybridCache(
    HybridCache hybridCache,
    ITenantService tenantService,
    IFhirSubscriptionRepository fhirSubscriptionRepository):
    IActiveSubscriptionCache
{ 

    const string CacheKey = "ActiveSubscriptions";
    const string CacheTag = "ActiveSubscriptions";
    
    public async Task<ICollection<ActiveSubscription>> GetList()
    {
        return await hybridCache.GetOrCreateAsync<ICollection<ActiveSubscription>>(
            key: GetKey(), 
            factory: async _ => await fhirSubscriptionRepository.GetActiveSubscriptionList(cancellationToken: CancellationToken.None), 
            options: new HybridCacheEntryOptions(),
            tags: GetCacheTags(), 
            cancellationToken: CancellationToken.None);
    }
    
    public async Task RefreshCache()
    {
        await hybridCache.RemoveByTagAsync(tags: GetCacheTags());
        await GetList();
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