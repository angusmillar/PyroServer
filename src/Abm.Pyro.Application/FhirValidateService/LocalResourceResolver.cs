using System.Net;
using System.Text;
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
    public async Task<Resource> ResolveByUriAsync(
        string uri)
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

    public async Task<Resource> ResolveByCanonicalUriAsync(
        string uri)
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