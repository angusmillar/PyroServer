using FluentResults;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using FhirUri = Abm.Pyro.Domain.FhirSupport.FhirUri;

namespace Abm.Pyro.Application.FhirBundleService;

public interface IFhirBundleCommonSupport
{
    FhirUri ParseBundleRequestFhirUriOrThrow(Bundle.EntryComponent entry);
    Result<FhirUri> ParseFhirUri(string uri);
    FhirUri ParseFhirUriOrThrowErrorMessage(string url, string errorMessage);
    string? GetHeaderValue(IReadOnlyDictionary<string, StringValues> headers,
        string headerName); 
    string SetResourceReference(
        FhirUri resourceReferenceFhirUri,
        string resourceName,
        string resourceId,
        int versionId);

}