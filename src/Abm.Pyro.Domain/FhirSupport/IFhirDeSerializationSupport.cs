using Hl7.Fhir.Model;
namespace Abm.Pyro.Domain.FhirSupport;

public interface IFhirDeSerializationSupport
{
  Resource? ToResource(string jsonResource);
  Task<Resource?> ToResource(Stream jsonStream);
}
