using System.Text.Json;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.ServiceSettings;
using Microsoft.Extensions.Caching.Hybrid;

namespace Abm.Pyro.Application.Cache;

public class ServiceSettingsCache(
  HybridCache hybridCache,
  ITenantService tenantService,
  IServiceConfigurationGetCurrentByType serviceConfigurationGetCurrentByType) : IServiceSettingsCache
{
  const string CacheClassCode = "ServiceSetting";

  public async Task<FhirValidationSettings> GetFhirValidationSettings()
  {
    return await GetByType<FhirValidationSettings>(serviceSettingType: ServiceSettingTypeId.FhirValidation);
  }
  
  private async Task<T> GetByType<T>(ServiceSettingTypeId serviceSettingType) where T : ServiceSettingsBase 
  {
    return await hybridCache.GetOrCreateAsync<T>(
      key: GetKey(serviceSettingTypeId: serviceSettingType), 
      factory: async _ => await GetServiceSettings<T>(serviceSettingType), 
      options: new HybridCacheEntryOptions(),
      tags: GetCacheTags());
  }

  public async Task Remove(ServiceSettingTypeId serviceSettingType)
  {
    await hybridCache.RemoveAsync(key: GetKey(serviceSettingType));
  }

  private async Task<T> GetServiceSettings<T>(ServiceSettingTypeId serviceSettingType) where T : ServiceSettingsBase 
  {
    var serviceSetting =  await serviceConfigurationGetCurrentByType.Get(typeId: serviceSettingType);
    T?  typedServiceSetting = JsonSerializer.Deserialize<T>(serviceSetting.Json);
    ArgumentNullException.ThrowIfNull(typedServiceSetting);
    return typedServiceSetting;
  }

  private string GetKey(ServiceSettingTypeId serviceSettingTypeId)
  {
    return CacheSupport.GetCacheKey(tenantCode: tenantService.GetScopedTenant().Code, key: $"{CacheClassCode}:{serviceSettingTypeId.ToString()}");
  }
  
  private string[] GetCacheTags()
  {
    return [tenantService.GetScopedTenant().Code, CacheClassCode];
  }
  
}
