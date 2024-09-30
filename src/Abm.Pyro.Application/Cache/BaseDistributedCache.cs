

using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using System.Text.Json;
using Abm.Pyro.Application.Tenant;

namespace Abm.Pyro.Application.Cache;

  public abstract class BaseDistributedCache<T>(
    IDistributedCache distributedCache, 
    ITenantService tenantService)
    where T : class
  {
    protected readonly ITenantService TenantService = tenantService;
    protected async Task<T?> TryGetValueAsync(string key)
    {
      byte[]? item = await distributedCache.GetAsync(TenantKey(key));
      if (item is not null)
      {
        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(item), GetJsonSerializerOptions());        
      }
      return null;
    }

    protected async Task SetAsync(string key, T value, DistributedCacheEntryOptions options)
    {
      byte[] Bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, GetJsonSerializerOptions()));
      await distributedCache.SetAsync(TenantKey(key), Bytes, options);      
    }

    protected async Task RemoveAsync(string key)
    {      
      await distributedCache.RemoveAsync(TenantKey(key));
    }

    private static JsonSerializerOptions GetJsonSerializerOptions()
    {
      return new JsonSerializerOptions()
             {        
               WriteIndented = false,                
             };
    }

    private string TenantKey(string key)
    {
      return $"{TenantService.GetScopedTenant().Code}:{key}";
    }
  }
 
