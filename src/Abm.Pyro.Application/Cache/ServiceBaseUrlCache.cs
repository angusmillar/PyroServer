using System.Net;
using Microsoft.Extensions.Caching.Distributed;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.TenantService;

namespace Abm.Pyro.Application.Cache;

public class ServiceBaseUrlCache(
  IDistributedCache distributedCache,
  IServiceBaseUrlGetByUri serviceBaseUrlGetByUri,
  IServiceBaseUrlGetPrimary serviceBaseUrlGetPrimary,
  ITenantService tenantService)
  : BaseDistributedCache<ServiceBaseUrl>(distributedCache, tenantService), IServiceBaseUrlCache
{
  private readonly Dictionary<string, ServiceBaseUrl> _primaryServiceBaseUrlDictionary = new ();

  public async Task<ServiceBaseUrl?> GetPrimaryAsync()
  {
    if (_primaryServiceBaseUrlDictionary.TryGetValue(TenantService.GetScopedTenant().Code, out ServiceBaseUrl? primaryServiceBaseUrl ))
    {
      return primaryServiceBaseUrl;
    }
    
    string primaryKey = GetPrimaryKey();
    primaryServiceBaseUrl = await TryGetValueAsync(primaryKey);
    if (primaryServiceBaseUrl is null)
    {
      primaryServiceBaseUrl = await serviceBaseUrlGetPrimary.Get();
      if (primaryServiceBaseUrl is not null)
      {
        await SetAsync(primaryKey, primaryServiceBaseUrl);
        _primaryServiceBaseUrlDictionary.Add(TenantService.GetScopedTenant().Code, primaryServiceBaseUrl);
      }
    }
    return primaryServiceBaseUrl;
  }
  public async Task<ServiceBaseUrl> GetRequiredPrimaryAsync()
  {
    if (_primaryServiceBaseUrlDictionary.TryGetValue(TenantService.GetScopedTenant().Code, out ServiceBaseUrl? primaryServiceBaseUrl ))
    {
      return primaryServiceBaseUrl;
    }
    
    primaryServiceBaseUrl = await GetPrimaryAsync();
    if (primaryServiceBaseUrl is not null)
    {
      return primaryServiceBaseUrl;
    }
    throw new FhirFatalException(
      HttpStatusCode.InternalServerError, "There was no primary service base URL found for the server. " +
                                          "This could occur if you are searching upon a server which has never has a any FHIR resource committed to its database. ");
  }

  public async Task<ServiceBaseUrl?> GetByUrlAsync(string url)
  {
    string key = GetKey(url);
    ServiceBaseUrl? serviceBaseUrl = await TryGetValueAsync(key);
    if (serviceBaseUrl is null)
    {
      serviceBaseUrl = await serviceBaseUrlGetByUri.Get(url);
      if (serviceBaseUrl is not null)
      {
        await SetAsync(key, serviceBaseUrl);
      }
    }
    return serviceBaseUrl;
  }

  private async Task SetAsync(string key, ServiceBaseUrl serviceBaseUrl)
  {
    await SetAsync(key, serviceBaseUrl, GetDistributedCacheEntryOptions());
  }

  public async Task Remove(string url)
  {
    await RemoveAsync(GetKey(url));
  }

  public async Task RemovePrimary()
  {
    _primaryServiceBaseUrlDictionary.Remove(TenantService.GetScopedTenant().Code);
    await RemoveAsync(GetPrimaryKey());
  }

  private static string GetKey(string url)
  {
    return url;
  }

  private static string GetPrimaryKey()
  {
    return "PRIMARY";
  }

  private static DistributedCacheEntryOptions GetDistributedCacheEntryOptions()
  {
    return new DistributedCacheEntryOptions() {
                                                SlidingExpiration = TimeSpan.FromMinutes(60),           //Will expire the entry if it hasn’t been accessed in a set amount of time.
                                                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(5) //Will expire the entry after a set amount of time.                                                                
                                              };
  }
}
