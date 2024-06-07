using System.Text.Json;
using Abm.Pyro.Domain.Exceptions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirDeSerializationSupport(IFhirJsonSerializersOptions fhirJsonSerializersOptions) : IFhirDeSerializationSupport
{
  public Resource? ToResource(string jsonResource)
  {
    try
    {
      return JsonSerializer.Deserialize<Resource>(jsonResource, fhirJsonSerializersOptions.ForDeserialization());

    }
    catch (DeserializationFailedException exception)
    {
      throw new FhirFatalException(System.Net.HttpStatusCode.BadRequest, "FHIR Json string parsing failed: " + exception.Message);
    }
  }
  
  public async Task<Resource?> ToResource(Stream jsonStream)
  {
    try
    {
      return await JsonSerializer.DeserializeAsync<Resource>(jsonStream, fhirJsonSerializersOptions.ForDeserialization());

    }
    catch (DeserializationFailedException exception)
    {
      throw new FhirFatalException(System.Net.HttpStatusCode.BadRequest, "FHIR JSON stream parsing failed: " + exception.Message);
    }
    catch (JsonException jasJsonException)
    {
      throw new FhirFatalException(System.Net.HttpStatusCode.BadRequest, "JSON stream parsing failed: " + jasJsonException.Message);
    }
  } 
}
