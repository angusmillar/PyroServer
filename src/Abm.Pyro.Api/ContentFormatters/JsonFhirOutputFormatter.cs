using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System.Text;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirSupport;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.ContentFormatters
{
  public class JsonFhirOutputFormatter : FhirMediaTypeOutputFormatter
  {
    public JsonFhirOutputFormatter()
    {
      foreach (var mediaType in Hl7.Fhir.Rest.ContentType.JSON_CONTENT_HEADERS)
        SupportedMediaTypes.Add(new MediaTypeHeaderValue(mediaType));
    }

    public override void WriteResponseHeaders(OutputFormatterWriteContext context)
    {   
      if (context is null)
        throw new ArgumentNullException(nameof(context));

      if (context.ObjectType is not null)
      {
        context.ContentType = FhirMediaType.GetMediaTypeHeaderValue(context.ObjectType, FhirFormatType.Json);  
      }
      
      // note that the base is called last, as this may overwrite the ContentType where the resource is of type Binary
      base.WriteResponseHeaders(context);
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
      if (context == null)
        throw new ArgumentNullException(nameof(context));

      if (context.Object is Resource resource)
      {
        Hl7.Fhir.Rest.SummaryType? summaryType = GetFhirSummaryType(resource);
        IFhirSerializationSupport fhirSerializationSupport = context.HttpContext.RequestServices.GetRequiredService<IFhirSerializationSupport>();
        await context.HttpContext.Response.WriteAsync(fhirSerializationSupport.ToJson(resource: resource, summaryType, true ));
      }
    }
    
  }
}