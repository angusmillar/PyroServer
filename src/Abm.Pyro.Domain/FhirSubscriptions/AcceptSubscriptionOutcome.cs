using Hl7.Fhir.Model;

namespace Abm.Pyro.Domain.FhirSubscriptions;

public record AcceptSubscriptionOutcome(bool Success, OperationOutcome? OperationOutcome = null);