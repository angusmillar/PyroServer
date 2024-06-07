using Abm.Pyro.Domain.Enums;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirRequestHttpHeaderSupport
{
    int? GetIfMatch(Dictionary<string, StringValues> requestHeaders);

    int? GetIfNoneMatch(Dictionary<string, StringValues> requestHeaders);
    
    string? GetXRequestId(Dictionary<string, StringValues> requestHeaders);
    
    string? GetIfNoneExist(Dictionary<string, StringValues> requestHeaders);

    DateTime? GetIfModifiedSince(Dictionary<string, StringValues> requestHeaders);

    DateTime? GetLastModified(Dictionary<string, StringValues> requestHeaders);
    
    Dictionary<string, StringValues> GetRequestHeadersFromBundleEntryRequest(Bundle.RequestComponent postEntryRequest);
    
    PreferHandlingType GetPreferHandling(Dictionary<string, StringValues> requestHeaders);

    public PreferReturnType GetPreferReturn(Dictionary<string, StringValues> requestHeaders);

    List<(string name, string value)> AllHeadersHumanDisplay(Dictionary<string, StringValues> requestHeaders);
}