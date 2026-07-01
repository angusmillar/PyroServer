using System.Net;
using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.Validation;

public class ConditionalPatchRequestValidator(
    ITenantService tenantService,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport)
    : ValidatorBase<FhirConditionalPatchRequest>(operationOutcomeSupport)
{
    private readonly IOperationOutcomeSupport OperationOutcomeSupport = operationOutcomeSupport;

    public override ValidatorResult Validate(FhirConditionalPatchRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), item.ResourceName).AllowConditionalPatch)
            return GetFailedEndpointPolicyValidatorResult();

        if (item.Resource is not Parameters)
        {
            return new ValidatorResult(
                isValid: false,
                httpStatusCode: HttpStatusCode.BadRequest,
                operationOutcome: OperationOutcomeSupport.GetError(
                [
                    $"The body of a conditional PATCH request must be a FHIR Parameters resource " +
                    $"(resourceType 'Parameters'), but received '{item.Resource.TypeName}'."
                ]));
        }

        if (string.IsNullOrWhiteSpace(item.QueryString))
        {
            return new ValidatorResult(
                isValid: false,
                httpStatusCode: HttpStatusCode.BadRequest,
                operationOutcome: OperationOutcomeSupport.GetError(
                [
                    "A conditional PATCH request requires at least one search parameter in the query string."
                ]));
        }

        return GetValidatorResult();
    }
}
