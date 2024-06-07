using System.Globalization;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Support;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirRequestHttpHeaderSupport : IFhirRequestHttpHeaderSupport
{
    private const string IfModifiedSinceStringFormat = "r";
    public int? GetIfMatch(Dictionary<string, StringValues> requestHeaders)
    {
        var values = TryGetHeader(requestHeaders:requestHeaders, headerName: HttpHeaderName.IfMatch);
        if (values.Count == 0 || values.First() is null)
        {
            return null;
        }
        
        return GetETag(values);
    }

    public int? GetIfNoneMatch(Dictionary<string, StringValues> requestHeaders)
    {
        var values = TryGetHeader(requestHeaders:requestHeaders, headerName: HttpHeaderName.IfNoneMatch);
        if (values.Count == 0 || values.First() is null)
        {
            return null;
        }

        return GetETag(values);
    }

    public string? GetXRequestId(Dictionary<string, StringValues> requestHeaders)
    {
        string headerName = "X-Request-Id";
        var values = TryGetHeader(requestHeaders:requestHeaders, headerName: headerName);
        if (values.Count == 0 || values.First() is null)
        {
            return null;
        }
        
        return values.First() ?? string.Empty;
    }

    private static int? GetETag(StringValues values)
    {
        //e.g W/"5"
        string[] versionEtagSplit = values.First()!.Split('"');
        if (versionEtagSplit.Length == 3)
        {
            if (Int32.TryParse(versionEtagSplit[1], out int versionId))
            {
                return versionId;
            }
        }

        return null;
    }

    public string? GetIfNoneExist(Dictionary<string, StringValues> requestHeaders)
    {
        var values = TryGetHeader(requestHeaders:requestHeaders, headerName: HttpHeaderName.IfNoneExist);
        if (values.Count == 0 || values.First() is null)
        {
            return null;
        }
        
        return values.First() ?? string.Empty;
    }

    public PreferHandlingType GetPreferHandling(Dictionary<string, StringValues> requestHeaders)
    {
        var values = TryGetHeader(requestHeaders:requestHeaders, headerName: HttpHeaderName.Prefer);
        if (values.Count == 0 || values.First() is null)
        {
            return PreferHandlingType.Lenient;
        }

        string termHandling = "Handling";
        //We should not get many but if we do we will just use the last one
        foreach (string value in values.Reverse().Where(x => x is not null).Select(x => x!))
        {
            if (value.Equals($"{termHandling}={PreferHandlingType.Strict.GetCode()}", StringComparison.OrdinalIgnoreCase))
            {
                return PreferHandlingType.Strict;
            }

            if (value.Equals($"{termHandling}={PreferHandlingType.Lenient.GetCode()}", StringComparison.OrdinalIgnoreCase))
            {
                return PreferHandlingType.Lenient;
            }
        }

        return PreferHandlingType.Lenient;
    }
    
    public PreferReturnType GetPreferReturn(Dictionary<string, StringValues> requestHeaders)
    {
        var values = TryGetHeader(requestHeaders:requestHeaders, headerName: HttpHeaderName.Prefer);
        if (values.Count == 0 || values.First() is null)
        {
            return PreferReturnType.Representation;
        }

        string termRetrun = "return";
        //We should not get many but if we do we will just use the last one
        foreach (string value in values.Reverse().Where(x => x is not null).Select(x => x!))
        {
            if (value.Equals($"{termRetrun}={PreferReturnType.Minimal.GetCode()}", StringComparison.OrdinalIgnoreCase))
            {
                return PreferReturnType.Minimal;
            }
            
            if (value.Equals($"{termRetrun}={PreferReturnType.Representation.GetCode()}", StringComparison.OrdinalIgnoreCase))
            {
                return PreferReturnType.Representation;
            }
            
            if (value.Equals($"{termRetrun}={PreferReturnType.OperationOutcome.GetCode()}", StringComparison.OrdinalIgnoreCase))
            {
                return PreferReturnType.OperationOutcome;
            }
        }

        return PreferReturnType.Representation;
    }
    
    public DateTime? GetIfModifiedSince(Dictionary<string, StringValues> requestHeaders)
    {
        var values = TryGetHeader(requestHeaders:requestHeaders, headerName: HttpHeaderName.IfModifiedSince);
        if (values.Count == 0 || values.First() is null)
        {
            return null;
        }
        
        //We should not get many but if we do we will just use the last one
        foreach (string value in values.Reverse().Where(x => x is not null).Select(x => x!))
        {
            if (DateTime.TryParseExact(value, IfModifiedSinceStringFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dateTime))
            {
                return dateTime;
            }
        }

        return null;
    }
    
    public DateTime? GetLastModified(Dictionary<string, StringValues> requestHeaders)
    {
        var values = TryGetHeader(requestHeaders:requestHeaders, headerName: HttpHeaderName.LastModified);
        if (values.Count == 0 || values.First() is null)
        {
            return null;
        }
        
        //We should not get many but if we do we will just use the last one
        foreach (string value in values.Reverse().Where(x => x is not null).Select(x => x!))
        {
            if (DateTime.TryParseExact(value, IfModifiedSinceStringFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dateTime))
            {
                return dateTime;
            }
        }

        return null;
    }

    public List<(string name, string value)> AllHeadersHumanDisplay(Dictionary<string, StringValues> requestHeaders)
    {
        var result = new List<(string name, string value)>();
        foreach (var header in requestHeaders)
        {
            foreach (var value in header.Value)
            {
                result.Add(new ValueTuple<string, string>(header.Key, value ?? string.Empty));
            }
        }

        return result;
    }

    public Dictionary<string, StringValues> GetRequestHeadersFromBundleEntryRequest(Bundle.RequestComponent postEntryRequest)
    {
        var result = new Dictionary<string, StringValues>();
        
        if (!string.IsNullOrWhiteSpace(postEntryRequest.IfNoneMatch))
        {
            result[HttpHeaderName.IfNoneMatch] = postEntryRequest.IfNoneMatch;
        }
        
        if (postEntryRequest.IfModifiedSince is not null)
        {
            result[HttpHeaderName.IfModifiedSince] = GetIfModifiedSince(postEntryRequest.IfModifiedSince);
        }
        
        if (!string.IsNullOrWhiteSpace(postEntryRequest.IfMatch))
        {
            result[HttpHeaderName.IfMatch] = postEntryRequest.IfMatch;
        }
        
        if (!string.IsNullOrWhiteSpace(postEntryRequest.IfNoneExist))
        {
            result[HttpHeaderName.IfNoneExist] = postEntryRequest.IfNoneExist;
        }

        return result;
    }

    private string? GetIfModifiedSince(DateTimeOffset? dateTimeOffset)
    {
        if (dateTimeOffset is null)
        {
            return null;
        }

        return dateTimeOffset.Value.UtcDateTime.ToString(IfModifiedSinceStringFormat);
    }

    private StringValues TryGetHeader(Dictionary<string, StringValues> requestHeaders, string headerName)
    {
        if (requestHeaders.TryGetValue(headerName, out var header))
        {
            return header;
        }

        return new StringValues();
    }
}