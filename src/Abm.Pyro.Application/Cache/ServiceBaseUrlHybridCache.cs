using System.Net;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Microsoft.Extensions.Caching.Hybrid;

namespace Abm.Pyro.Application.Cache;

public class ServiceBaseUrlHybridCache(
  HybridCache hybridCache,
  ITenantService tenantService,
  IServiceBaseUrlGetPrimary serviceBaseUrlGetPrimary,
  IServiceBaseUrlGetOrAddByUri serviceBaseUrlGetOrAddByUri)
  : IServiceBaseUrlCache
{
  
  const string CacheKeyForPrimary = "PrimaryBaseUrl";
  const string CacheKeyPrefixForUrl = "BaseUrl";
  const string CacheTag = "ServiceBaseUrls";
  private Dictionary<string, ServiceBaseUrl> ScopedServiceBaseUrlCacheDictionary { get; set; } = new();
    
  public async Task<ServiceBaseUrl?> GetPrimaryAsync()
  {
    return await hybridCache.GetOrCreateAsync<ServiceBaseUrl?>(
      key: GetPrimaryKey(), 
      factory: async _ => await serviceBaseUrlGetPrimary.Get(), 
      options: new HybridCacheEntryOptions(),
      tags: GetCacheTags(), 
      cancellationToken: CancellationToken.None);
  }

  public async Task<ServiceBaseUrl> GetRequiredPrimaryAsync()
  {
    ServiceBaseUrl? primaryServiceBaseUrl = await GetPrimaryAsync();
    if (primaryServiceBaseUrl is null)
    {
      throw new FhirFatalException(
        HttpStatusCode.InternalServerError, 
        "There was no primary service base URL found for the server. " +
        "This could occur if you are searching upon a server which has never " +
        "has a any FHIR resource committed to its database. ");
    }
    return primaryServiceBaseUrl;
  }

  public async Task<ServiceBaseUrl?> GetByUrlAsync(string url)
  {
      ServiceBaseUrl serviceBaseUrl =  await hybridCache.GetOrCreateAsync<ServiceBaseUrl>(
      key: GetUrlKey(url), 
      factory: async _ => await serviceBaseUrlGetOrAddByUri.Get(url), 
      options: new HybridCacheEntryOptions(),
      tags: GetCacheTags(), 
      cancellationToken: CancellationToken.None);
      
      return serviceBaseUrl;
  }

  public async Task Remove(string url)
  {
    await hybridCache.RemoveAsync(GetUrlKey(url));
  }

  public async Task RemovePrimary()
  {
    await hybridCache.RemoveAsync(GetPrimaryKey());
  }

  private string GetPrimaryKey()
  {
    return CacheSupport.GetCacheKey(tenantCode: tenantService.GetScopedTenant().Code, key: CacheKeyForPrimary);
  }

  private string GetUrlKey(string url)
  {
    return CacheSupport.GetCacheKey(tenantCode: tenantService.GetScopedTenant().Code, key:$"{CacheKeyPrefixForUrl}:{url}");
  }
  
  private string[] GetCacheTags()
  {
    return [tenantService.GetScopedTenant().Code, CacheTag];
  }
  
}
