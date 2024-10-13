using FluentResults;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.FhirSubscriptions;

public record AcceptSubscriptionOutcome(bool Success, OperationOutcome? OperationOutcome = null);