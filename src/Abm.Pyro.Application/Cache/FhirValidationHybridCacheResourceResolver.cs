using System.Net;
using System.Text;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Primitives;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Cache;

public class FhirValidationHybridCacheResourceResolver(
    HybridCache hybridCache,
    ITenantService tenantService,
    IFhirSearchHandler fhirSearchHandler,
    IFhirUriFactory fhirUriFactory) : IFhirValidationHybridCacheResourceResolver, IAsyncResourceResolver
{ 

    const string CacheKey = "FhirVal";
    const string CacheTag = "FhirValidationResources";

    public async Task RefreshCache()
    {
        await hybridCache.RemoveByTagAsync(tags: GetCacheTags());
    }

    public Task<Resource> ResolveByUriAsync(
        string uri)
    {
        throw new NotImplementedException("Has not been implemented as never been called while validating in development.");
    }

    public async Task<Resource> ResolveByCanonicalUriAsync(string uri)
    {
        return await hybridCache.GetOrCreateAsync<Resource>(
            key: GetKey(uri), 
            factory: async _ => await ResolveByCanonicalUriAsyncX(uri: uri), 
            options: new HybridCacheEntryOptions(),
            tags: GetCacheTags(), 
            cancellationToken: CancellationToken.None);
    }
    private string GetKey(string uri)
    {
        return CacheSupport.GetCacheKey(tenantCode: tenantService.GetScopedTenant().Code, key: $"{CacheKey}:{uri}");
    }

    private string[] GetCacheTags()
    {
        return [tenantService.GetScopedTenant().Code, CacheTag];
    }
    
    private async Task<Resource> ResolveByCanonicalUriAsyncX(string uri)
    {
        
        var fhirUriFactoryResult = await fhirUriFactory.TryParse2(requestUri: uri);

        if (!fhirUriFactoryResult.Success)
        {
            return new Bundle();
        }

        ArgumentNullException.ThrowIfNull(fhirUriFactoryResult.fhirUri);
        if (string.IsNullOrWhiteSpace(fhirUriFactoryResult.fhirUri.ResourceName))
        {
            return new Bundle();
        }
        
        FhirResourceResponse searchResponse = await fhirSearchHandler.Handle(
            tenant: tenantService.GetScopedTenantCode(),
            requestId: GuidSupport.NewFhirGuid(),
            resourceName: fhirUriFactoryResult.fhirUri.ResourceName,
            query: GetQuery(fhirUriFactoryResult.fhirUri.OriginalString, fhirUriFactoryResult.fhirUri.CanonicalVersionId),
            headers: new Dictionary<string, StringValues>(),
            cancellationToken: CancellationToken.None);

        if (searchResponse.HttpStatusCode == HttpStatusCode.OK &&
            searchResponse.Resource is Bundle bundle)
        {
            return bundle.Entry[0].Resource;
        }

        return searchResponse.Resource;
    }

    private static string GetQuery(string urlOriginalString, string canonicalVersionId)
    {
        var queryBuilder = new StringBuilder("url=");
        if (string.IsNullOrWhiteSpace(canonicalVersionId))
        {
            queryBuilder.Append(urlOriginalString);
            return queryBuilder.ToString();
        }

        string urlWithoutCanonicalVersionId = urlOriginalString.TrimEnd($"|{canonicalVersionId}".ToCharArray());
        queryBuilder.Append(urlWithoutCanonicalVersionId);
        queryBuilder.Append('&');
        queryBuilder.Append($"version={canonicalVersionId}");

        return queryBuilder.ToString();
    }
}