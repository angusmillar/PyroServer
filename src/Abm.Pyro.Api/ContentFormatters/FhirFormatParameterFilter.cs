using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace Abm.Pyro.Api.ContentFormatters
{
  public class FhirFormatParameterFilter : IResultFilter
  {
    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
      // Look for the _format parameter on the query
      var query = context.HttpContext.Request.Query;
      if (query?.ContainsKey("_format") == true)
      {
        if (string.Equals(query["_format"], "xml", StringComparison.OrdinalIgnoreCase))
        {
          context.HttpContext.Request.Headers[HeaderNames.Accept] = new string[] { FhirMediaType.XmlResource };
        }

        if (string.Equals(query["_format"], "json", StringComparison.OrdinalIgnoreCase))
        {
          context.HttpContext.Request.Headers[HeaderNames.Accept] = new string[] { FhirMediaType.JsonResource };
        }
      }  
    }

  }
}
