namespace Abm.Pyro.Api.Extensions;

public static class HttpRequestExtensions
{
  public static Uri OriginalRequestUrl(this HttpRequest req)
  {
    var uriBuilder = new UriBuilder(req.Scheme, req.Host.Host, req.Host.Port ?? -1, req.Path);
    if (uriBuilder.Uri.IsDefaultPort)
    {
      uriBuilder.Port = -1;
    }
    if (req.QueryString.HasValue)
    {
      uriBuilder.Query = req.QueryString.Value;  
    }
    
    return uriBuilder.Uri;
  }
}
