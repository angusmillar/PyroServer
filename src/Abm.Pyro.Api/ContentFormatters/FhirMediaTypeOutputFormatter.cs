using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Abm.Pyro.Domain.Exceptions;

namespace Abm.Pyro.Api.ContentFormatters
{
  public abstract class FhirMediaTypeOutputFormatter : TextOutputFormatter
  {
    protected FhirMediaTypeOutputFormatter()
    {
      this.SupportedEncodings.Clear();
      this.SupportedEncodings.Add(Encoding.UTF8);
    }

    protected override bool CanWriteType(Type? type)
    {
      if (typeof(Resource).IsAssignableFrom(type))
        return true;
      
      // The null case here is to support the deleted FhirObjectResult
      if (type is null)
        return true;
      
      return false;
    }

    public override void WriteResponseHeaders(OutputFormatterWriteContext context)
    {
      if (context is null)
        throw new ArgumentNullException(nameof(context));

      base.WriteResponseHeaders(context);
      WriteFhirBinaryResourceContentTypeIfProvided(context);
    }

    private static void WriteFhirBinaryResourceContentTypeIfProvided(OutputFormatterWriteContext context)
    {
      if (context.Object is not Binary binaryResource) return;
      if (!string.IsNullOrWhiteSpace(binaryResource.ContentType))
      {
        context.HttpContext.Response.Headers[HeaderNames.ContentType] = binaryResource.ContentType;
        context.ContentType =
          new Microsoft.Extensions.Primitives.StringSegment(binaryResource.ContentType);  
      }
    }
    
    protected static Hl7.Fhir.Rest.SummaryType? GetFhirSummaryType(Resource resource)
    {
      if (resource.HasAnnotation<Hl7.Fhir.Rest.SummaryType>())
      {
        return resource.Annotation<Hl7.Fhir.Rest.SummaryType>();
      }
      return null;
    }
    
    protected static SerializationFilter? GetSerializationFilter(Hl7.Fhir.Rest.SummaryType? summaryType)
    {
      return summaryType switch
      {
        Hl7.Fhir.Rest.SummaryType.True => SerializationFilter.ForSummary(),
        Hl7.Fhir.Rest.SummaryType.Text => SerializationFilter.ForText(),
        Hl7.Fhir.Rest.SummaryType.Data => SerializationFilter.ForData(),
        Hl7.Fhir.Rest.SummaryType.Count => null,
        Hl7.Fhir.Rest.SummaryType.False => null,
        null => null,
        _ => throw new FhirFatalException(System.Net.HttpStatusCode.BadRequest,
          $"Unable to resolve SummaryType for value: {summaryType}.")
      };
    }

  }
}
