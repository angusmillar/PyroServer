using Hl7.Fhir.ElementModel;
namespace Abm.Pyro.Application.FhirResolver;

public interface IFhirPathResolve
{
  ITypedElement? Resolver(string url);
}
