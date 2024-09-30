using Abm.Pyro.Application.Tenant;
using Microsoft.Extensions.Caching.Distributed;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Projection;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Application.Cache;

public class SearchParameterCache(
  IDistributedCache distributedCache, 
  ISearchParameterGetByBaseResourceType searchParameterGetByBaseResourceType,
  ITenantService tenantService)
  : BaseDistributedCache<IEnumerable<SearchParameterProjection>>(distributedCache, tenantService), ISearchParameterCache
{
  public async Task<IEnumerable<SearchParameterProjection>> GetListByResourceType(FhirResourceTypeId resourceType)
  {
    string key = GetKey(resourceType);
    IEnumerable<SearchParameterProjection>? searchParameterList = await TryGetValueAsync(key);
    if (searchParameterList is null)
    {
      searchParameterList = await searchParameterGetByBaseResourceType.Get(resourceType);
      await SetAsync(key, searchParameterList);
    }
    return searchParameterList;
  }

  private async Task SetAsync(string key , IEnumerable<SearchParameterProjection> searchParameterList)
  {
    await SetAsync(key, searchParameterList, GetDistributedCacheEntryOptions());
  }
  
  public async Task Remove(FhirResourceTypeId resourceType)
  {
    await RemoveAsync(GetKey(resourceType));
  }

  private static string GetKey(FhirResourceTypeId resourceType)
  {
    return resourceType.GetCode();
  }

  private static DistributedCacheEntryOptions GetDistributedCacheEntryOptions()
  {
    return new DistributedCacheEntryOptions() {
                                                SlidingExpiration = TimeSpan.FromMinutes(60),           //Will expire the entry if it hasn’t been accessed in a set amount of time.
                                                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(5) //Will expire the entry after a set amount of time.                                                                
                                              };
  }
}
