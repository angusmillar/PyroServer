using Microsoft.Extensions.Primitives;

namespace Abm.Pyro.Api.Extensions;

public static class HttpHeaderExtension
{
  public static void AppendRange(this IHeaderDictionary headers, Dictionary<string, StringValues> headerDictionary)
  {
    foreach (var item in headerDictionary)
    {
      headers.Append(item.Key, item.Value);  
    }
  }
  
  public static Dictionary<string, StringValues> GetDictionary(this IHeaderDictionary headers)
  {
    var headerDictionary = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in headers)
    {
      headerDictionary.Add(item.Key, item.Value);  
    }
    return headerDictionary;
  }
}
