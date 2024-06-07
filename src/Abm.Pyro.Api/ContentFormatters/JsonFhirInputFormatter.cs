using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Diagnostics;
using System.Text;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Exceptions;
using Abm.Pyro.Domain.FhirSupport;
using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Api.ContentFormatters
{
  public class JsonFhirInputFormatter : FhirMediaTypeInputFormatter
  {
    public JsonFhirInputFormatter() 
    {
      foreach (var mediaType in Hl7.Fhir.Rest.ContentType.JSON_CONTENT_HEADERS)
        SupportedMediaTypes.Add(new MediaTypeHeaderValue(mediaType));
    }

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
    {
      CheckForUtf8Encoding(encoding);
      await DrainRequestBodyStream(context.HttpContext.Request);
      
      IFhirDeSerializationSupport fhirDeSerializationSupport = context.HttpContext.RequestServices.GetRequiredService<IFhirDeSerializationSupport>();
      Resource? resource = await fhirDeSerializationSupport.ToResource(context.HttpContext.Request.Body);
      return await InputFormatterResult.SuccessAsync(resource);
    }

    private static void CheckForUtf8Encoding(Encoding encoding)
    {
      if (encoding.EncodingName != Encoding.UTF8.EncodingName)
      {
        throw new FhirFatalException(System.Net.HttpStatusCode.BadRequest,
          "FHIR supports UTF-8 encoding exclusively. The encoding found was : " + encoding.WebName);
      }
    }

    private static async Task DrainRequestBodyStream(HttpRequest request)
    {
      // TODO: Brian: Would like to know what the issue is here? Will this be resolved by the Async update to the core?
      if (!request.Body.CanSeek)
      {
        // To avoid blocking on the stream, we asynchronously read everything 
        // into a buffer, and then seek back to the beginning.
        request.EnableBuffering();
        Debug.Assert(request.Body.CanSeek);

        // no timeout configuration on this? or does that happen at another layer?
        await request.Body.DrainAsync(CancellationToken.None);
        request.Body.Seek(0L, SeekOrigin.Begin);
      }
    }
    
  }
}
