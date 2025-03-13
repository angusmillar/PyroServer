using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirValidate;

public record FhirValidateRequest(FhirValidateMode? Mode, string? Profile, Resource? Resource);