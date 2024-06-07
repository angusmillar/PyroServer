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
        if (string.Compare(query["_format"], "xml", StringComparison.CurrentCultureIgnoreCase) == 0)
        {
          context.HttpContext.Request.Headers[HeaderNames.Accept] = new string[] { FhirMediaType.XmlResource };
        }

        if (string.Compare(query["_format"], "json", StringComparison.CurrentCultureIgnoreCase) == 0)
        {
          context.HttpContext.Request.Headers[HeaderNames.Accept] = new string[] { FhirMediaType.JsonResource };
        }
      }  
    }

  }
}
