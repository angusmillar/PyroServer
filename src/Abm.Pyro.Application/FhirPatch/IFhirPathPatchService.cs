using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.FhirPatch;

public interface IFhirPathPatchService
{
    Resource Apply(Resource target, Parameters patchParameters);
}
