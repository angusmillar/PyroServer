using Microsoft.Extensions.Primitives;
using System.Net.Http.Headers;
using System.Text;
using Hl7.Fhir.Model;
using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Api.ContentFormatters
{
  public static class FhirMediaType
  {
    // TODO: This class can be merged into HL7.Fhir.ContentType

    public const string XmlResource = "application/fhir+xml";
    public const string JsonResource = "application/fhir+json";
    public const string BinaryResource = "application/fhir+binary";

    public static FhirFormatType GetFhirFormatTypeFromAcceptHeader(string? acceptHeaderValue)
    {
      var defaultType = FhirFormatType.Json;
      if (string.IsNullOrWhiteSpace(acceptHeaderValue))
        return defaultType;

      Dictionary<string, FhirFormatType> mediaTypeDic = new Dictionary<string, FhirFormatType>();
      foreach (var mediaType in Hl7.Fhir.Rest.ContentType.XML_CONTENT_HEADERS)
        mediaTypeDic.Add(mediaType, FhirFormatType.Xml);

      foreach (var mediaType in Hl7.Fhir.Rest.ContentType.JSON_CONTENT_HEADERS)
        mediaTypeDic.Add(mediaType, FhirFormatType.Json);

      acceptHeaderValue = acceptHeaderValue.Trim();
      if (mediaTypeDic.TryGetValue(acceptHeaderValue, out var header))
      {
        return header;
      }
      return defaultType;
    }

    public static string GetContentType(Type type, FhirFormatType format)
    {
      if (typeof(Resource).IsAssignableFrom(type))
      {
        switch (format)
        {
          case FhirFormatType.Json: return JsonResource;
          case FhirFormatType.Xml: return XmlResource;
          default: return JsonResource;
        }
      }
      else
        return "application/octet-stream";
    }

    public static StringSegment GetMediaTypeHeaderValue(Type type, FhirFormatType format)
    {
      string mediaType = GetContentType(type, format);

      var header = new MediaTypeHeaderValue(mediaType) {
                                                         CharSet = Encoding.UTF8.WebName
                                                       };
      
      return new StringSegment($"{header}");
      
    }

  }
}
