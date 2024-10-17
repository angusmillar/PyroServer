using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Support;
using Abm.Pyro.Domain.TenantService;
using Microsoft.Extensions.Options;


namespace Abm.Pyro.Domain.ServiceBaseUrlService;

public class PrimaryServiceBaseUrlService(
    IServiceBaseUrlCache serviceBaseUrlCache,
    IOptions<ServiceBaseUrlSettings> serviceBaseUrlSettings,
    ITenantService tenantService) : IPrimaryServiceBaseUrlService
{
    private Model.ServiceBaseUrl? _serviceBaseUrl;
    private Uri? _serviceBaseUrlSettingsUri;
    
    
    public async Task<Domain.Model.ServiceBaseUrl> GetServiceBaseUrlAsync()
    {
       return await GetCachedServiceBaseUrl();
    }

    public async Task<string> GetUrlAsync()
    {
        return (await GetCachedServiceBaseUrl()).Url;
    }

    public async Task<Uri> GetUriAsync()
    {
        return new Uri((await GetCachedServiceBaseUrl()).Url);
    }
    
    public string GetUrlString()
    {
        return GetFromSettings().OriginalString;
    }

    public Uri GetUri()
    {
        return GetFromSettings();
    }

    
    //Lazy loading 
    private async Task<ServiceBaseUrl> GetCachedServiceBaseUrl()
    {
        if (_serviceBaseUrl is null)
        {
            _serviceBaseUrl = await serviceBaseUrlCache.GetRequiredPrimaryAsync();
            //That we hard code https here has no ill effect because the FhirServiceBaseUrlsAreEqual method ignores it when comparing
            //Yet a schema (http or https) must be added for the method to work correctly.
            if (!GetFromSettings().FhirServiceBaseUrlsAreEqual(new Uri($"https://{_serviceBaseUrl.Url}")))
            {
                throw new ApplicationException("While loaded the ServiceBaseUrl, the service base url based on appsettings is not equal to the service base url based on the ServiceBaseUrlCache.");
            }
        }

        return _serviceBaseUrl;
    }
    
    //Lazy loading 
    private Uri GetFromSettings()
    {
        if (_serviceBaseUrlSettingsUri is null)
        {
            _serviceBaseUrlSettingsUri = new Uri(serviceBaseUrlSettings.Value.Url, tenantService.GetScopedTenant().UrlCode);
            if (!GetUrlString().Equals(_serviceBaseUrlSettingsUri.OriginalString))
            {
                throw new ApplicationException("While loaded the ServiceBaseUrlSettings, The service base url based on appsettings is not equal to the service base url based on the ServiceBaseUrlCache.");
            }
        }

        return _serviceBaseUrlSettingsUri;
    }
}