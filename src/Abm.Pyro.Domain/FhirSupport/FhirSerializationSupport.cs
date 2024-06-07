using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
namespace Abm.Pyro.Domain.FhirSupport;

public class FhirSerializationSupport(IFhirJsonSerializersOptions fhirJsonSerializersOptions) : IFhirSerializationSupport
{
  public string ToJson(Resource resource, SummaryType? summaryType, bool pretty = false)
  {
    var options = fhirJsonSerializersOptions.ForSerialization(summaryType, pretty);
  
    return JsonSerializer.Serialize(resource, options);
  }
}
