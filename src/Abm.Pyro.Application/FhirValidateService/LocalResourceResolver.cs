using System.Net;
using Abm.Pyro.Application.FhirHandler;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.FhirSupport;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Application.FhirValidateService;

public class LocalResourceResolver(
    ITenantService tenantService,
    IFhirSearchHandler fhirSearchHandler,
    IFhirUriFactory fhirUriFactory) : IAsyncResourceResolver
{
    public async Task<Resource> ResolveByUriAsync(string uri)
    {
        FhirResourceResponse searchResponse = await fhirSearchHandler.Handle(
            tenant: tenantService.GetScopedTenantCode(),
            requestId: GuidSupport.NewFhirGuid(),
            resourceName: ResourceType.StructureDefinition.GetLiteral(),
            query: $"url={uri}",
            headers: new Dictionary<string, StringValues>(),
            cancellationToken: CancellationToken.None);
        
        return searchResponse.Resource;
    }

    public async Task<Resource> ResolveByCanonicalUriAsync(string uri)
    {
        var result = await fhirUriFactory.TryParse2(requestUri: uri);

        if (!result.Success)
        {
            throw new ArgumentException("Why would this fail");
        }
        
        ArgumentNullException.ThrowIfNull(result.fhirUri);

        if (string.IsNullOrWhiteSpace(result.fhirUri.ResourceName))
        {
            return new Bundle();
        }
        
        
        FhirResourceResponse searchResponse = await fhirSearchHandler.Handle(
            tenant: tenantService.GetScopedTenantCode(),
            requestId: GuidSupport.NewFhirGuid(),
            resourceName: result.fhirUri.ResourceName,
            query: $"url={result.fhirUri.OriginalString}",
            headers: new Dictionary<string, StringValues>(),
            cancellationToken: CancellationToken.None);

        if (searchResponse.HttpStatusCode == HttpStatusCode.OK &&
            searchResponse.Resource is Bundle bundle)
        {
            return bundle.Entry[0].Resource;
        }
        
        return searchResponse.Resource;
    }
}