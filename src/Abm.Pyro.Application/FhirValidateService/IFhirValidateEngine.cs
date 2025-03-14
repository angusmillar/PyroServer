using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.FhirValidateService;

public interface IFhirValidateEngine
{
    OperationOutcome Validate(Resource resource, List<Uri> profileUriList);
    OperationOutcome Validate(Resource resource);
}