using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.TenantService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abm.Pyro.Domain.ServiceBaseUrlService;

public class PrimaryServiceBaseUrlService(
    IServiceBaseUrlCache serviceBaseUrlCache,
    IOptions<ServiceBaseUrlSettings> serviceBaseUrlSettings,
    ITenantService tenantService) : IPrimaryServiceBaseUrlService
{
    private Uri? _serviceBaseUriFromAppSettings;
    private Uri? _serviceBaseUriFromDatabaseCache;
    
    public async Task<string> GetUrlAsync()
    {
        var serviceBaseUrl = await GetCachedServiceBaseUrl();
        return serviceBaseUrl.OriginalString;
    }

    public async Task<Uri> GetUriAsync()
    {
        return await GetCachedServiceBaseUrl();
    }
    
    public string GetUrlString()
    {
        return GetFromSettings().OriginalString;
    }

    public Uri GetUri()
    {
        return GetFromSettings();
    }

    
    //Lazy loading from database cache, and validated against that loaded from appsettings
    private async Task<Uri> GetCachedServiceBaseUrl()
    {
        if (_serviceBaseUriFromDatabaseCache is null)
        {
            ServiceBaseUrl serviceBaseUrlFromDatabaseCache = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
            Uri serviceBaseUrlFromSettings = GetFromSettings();
            _serviceBaseUriFromDatabaseCache = new Uri($"{serviceBaseUrlFromSettings.Scheme}://{serviceBaseUrlFromDatabaseCache.Url}");
            
            if (!serviceBaseUrlFromSettings.FhirServiceBaseUrlsAreEqual(_serviceBaseUriFromDatabaseCache))
            {
                throw new ApplicationException("While loaded the Service Base Url, the url provided in appsettings was " +
                                               "found to not equal the url provided by the application's database cache");
            }
        }

        return _serviceBaseUriFromDatabaseCache;
    }
    
    //Lazy loading from appsettings
    private Uri GetFromSettings()
    {
        if (_serviceBaseUriFromAppSettings is null)
        {
            _serviceBaseUriFromAppSettings = new Uri(serviceBaseUrlSettings.Value.Url, tenantService.GetScopedTenant().UrlCode);
        }

        return _serviceBaseUriFromAppSettings;
    }
}