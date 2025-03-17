using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.FhirValidateService;

public interface IFhirValidateEngine
{
    Task<OperationOutcome> Validate(Resource resource, List<Uri> profileUriList);
    Task<OperationOutcome> Validate(Resource resource);
}