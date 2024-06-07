namespace Abm.Pyro.Domain.Support;

public static class UriSupport
{
    public static bool FhirServiceBaseUrlsAreEqual(this Uri serversServiceBaseUrl, Uri uri)
    {
        if (!uri.IsAbsoluteUri || !serversServiceBaseUrl.IsAbsoluteUri)
        {
            throw new ApplicationException("Fhir Service Base Urls MUST be Absolute Uris");
        }
        
        return uri.Authority.Equals(serversServiceBaseUrl.Authority) && 
               uri.LocalPath.Equals(serversServiceBaseUrl.LocalPath, StringComparison.Ordinal);
    }
    
    public static string FhirServiceBaseUrlFormattedString(this Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            throw new ApplicationException("Fhir Service Base Url MUST be an absolute URI");
        }
        return uri.ToString().StripHttp().TrimEnd('/');
    }
}