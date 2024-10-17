using Abm.Pyro.Application.FhirSubscriptions;
using Abm.Pyro.Domain.TenantService;
using Microsoft.Extensions.Caching.Distributed;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Cache;

public class ActiveSubscriptionCache(
    
    IFhirSubscriptionRepository fhirSubscriptionRepository,
    
    IDistributedCache distributedCache,
    ITenantService tenantService)
    : BaseDistributedCache<ICollection<ActiveSubscription>>(distributedCache, tenantService), IActiveSubscriptionCache
{

    public async Task<ICollection<ActiveSubscription>> GetList()
    {
        string key = GetKey();
        ICollection<ActiveSubscription>? activeSubscriptionList = await TryGetValueAsync(key);
        if (activeSubscriptionList is null)
        {
            activeSubscriptionList = await fhirSubscriptionRepository.GetActiveSubscriptionList(cancellationToken: default);
            await SetAsync(key, activeSubscriptionList);
        }
        return activeSubscriptionList;
    }
    
    public async Task RefreshCache()
    {
        await RemoveAsync(GetKey());
        await GetList();
    }
    
    private async Task SetAsync(string key, ICollection<ActiveSubscription> activeSubscriptionList)
    {
        await SetAsync(key, activeSubscriptionList, GetDistributedCacheEntryOptions());
    }
    
    private static DistributedCacheEntryOptions GetDistributedCacheEntryOptions()
    {
        return new DistributedCacheEntryOptions() {
            SlidingExpiration = TimeSpan.FromHours(12),           //Will expire the entry if it has not been accessed in a set amount of time.
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) //Will expire the entry after a set amount of time.                                                                
        };
    }
    
    private static string GetKey()
    {
        return $"SUB";
    }
}