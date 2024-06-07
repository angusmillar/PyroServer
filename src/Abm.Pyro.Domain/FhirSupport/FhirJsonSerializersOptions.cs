using System.Text.Json;
using Abm.Pyro.Domain.Exceptions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;

namespace Abm.Pyro.Domain.FhirSupport;

public class FhirJsonSerializersOptions : IFhirJsonSerializersOptions
{
  private readonly JsonSerializerOptions Options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

  public JsonSerializerOptions ForDeserialization()
  {
    return Options;
  }
  
  public JsonSerializerOptions ForSerialization(SummaryType? summaryType, bool pretty = false)
  {
    var settings = new FhirJsonPocoSerializerSettings()
                   {
                     SummaryFilter = GetSerializationFilter(summaryType)
                   };
    
     return pretty ? Options.ForFhir(settings).Pretty() : Options.ForFhir(settings);
  }
  
  private static SerializationFilter? GetSerializationFilter(SummaryType? summaryType)
  {
    return summaryType switch
    {
      SummaryType.True => SerializationFilter.ForSummary(),
      SummaryType.Text => SerializationFilter.ForText(),
      SummaryType.Data => SerializationFilter.ForData(),
      SummaryType.Count => null,
      SummaryType.False => null,
      null => null,
      _ => throw new FhirFatalException(System.Net.HttpStatusCode.BadRequest,
                                        $"Unable to resolve SummaryType for value: {summaryType}.")
    };
  }
}
