using Hl7.Fhir.Model;
namespace Abm.Pyro.Application.FhirResolver;

public interface IFhirPathResolve
{
  PocoNode Resolver(string url);
}
