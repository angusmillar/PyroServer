using System.Net;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirSupport;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;
using FluentResults; 

namespace Abm.Pyro.Application.FhirBundleService;

public class FhirBundleCommonSupport(IFhirUriFactory fhirUriFactory) : IFhirBundleCommonSupport
{
    
    
    public FhirUri ParseBundleRequestFhirUriOrThrow(Bundle.EntryComponent entry)
    {
        if (entry.Request?.Url is null)
        {
            throw new FhirErrorException(httpStatusCode: HttpStatusCode.BadRequest, $"The entry with fullUrl: {entry.FullUrl} must contains a entry.request element.");
        }
        return ParseFhirUriOrThrowErrorMessage(url: entry.Request.Url, errorMessage: $"Unable to parse bundle.entry[x].request.url of: {entry.Request.Url}");
    }
    
    public Result<FhirUri> ParseBundleRequestFhirUri(Bundle.EntryComponent entry)
    {
        if (entry.Request?.Url is null)
        {
            throw new FhirErrorException(httpStatusCode: HttpStatusCode.BadRequest, $"The entry with fullUrl: {entry.FullUrl} must contains a entry.request element.");
        }
        return ParseFhirUriOrThrowErrorMessage(url: entry.Request.Url, errorMessage: $"Unable to parse bundle.entry[x].request.url of: {entry.Request.Url}");
    }
    
    public Result<FhirUri> ParseFhirUri(string uri)
    {
        if (!fhirUriFactory.TryParse(uri, out FhirUri? fhirUri, out string parseErrorMessage))
        {
            return Result.Fail(parseErrorMessage);
        }

        return Result.Ok(fhirUri);
    }
    
    public FhirUri ParseFhirUriOrThrowErrorMessage(string url, string errorMessage)
    {
        if (!fhirUriFactory.TryParse(url, out FhirUri? fhirUri, out string parseErrorMessage))
        {
            throw new FhirErrorException(httpStatusCode: HttpStatusCode.BadRequest, $"{errorMessage} {parseErrorMessage}");
        }

        return fhirUri;
    }
    
    
    
    public string? GetHeaderValue(IReadOnlyDictionary<string, StringValues> headers, string headerName)
    {
        if (headers.TryGetValue(headerName, out StringValues values))
        {
            return values;
        }

        return null;
    }
    
    public string SetResourceReference(
        FhirUri resourceReferenceFhirUri,
        string resourceName,
        string resourceId,
        int versionId)
    {
        if (resourceReferenceFhirUri.IsAbsoluteUri)
        {
            if (resourceReferenceFhirUri.IsHistoryReference)
            {
                return $"{resourceReferenceFhirUri.PrimaryServiceRootServers.OriginalString}/{resourceName}/{resourceId}/_history/{versionId.ToString()}";
            }

            return $"{resourceReferenceFhirUri.PrimaryServiceRootServers.OriginalString}/{resourceName}/{resourceId}";
        }

        if (resourceReferenceFhirUri.IsHistoryReference)
        {
            return $"{resourceName}/{resourceId}/_history/{versionId.ToString()}";
        }

        return $"{resourceName}/{resourceId}";
    }
}