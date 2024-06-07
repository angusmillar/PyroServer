using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirSerializationSupport
{
  string ToJson(Resource resource, SummaryType? summaryType, bool pretty = false);
}
