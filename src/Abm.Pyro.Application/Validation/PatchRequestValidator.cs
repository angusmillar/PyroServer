using System.Net;
using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.Validation;

public class PatchRequestValidator(
    ITenantService tenantService,
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport)
    : ValidatorBase<FhirPatchRequest>(operationOutcomeSupport)
{
    private readonly IOperationOutcomeSupport OperationOutcomeSupport = operationOutcomeSupport;

    public override ValidatorResult Validate(FhirPatchRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), item.ResourceName).AllowPatch)
            return GetFailedEndpointPolicyValidatorResult();

        if (item.Resource is not Parameters)
        {
            return new ValidatorResult(
                isValid: false,
                httpStatusCode: HttpStatusCode.BadRequest,
                operationOutcome: OperationOutcomeSupport.GetError(
                [
                    $"The body of a PATCH request must be a FHIR Parameters resource " +
                    $"(resourceType 'Parameters'), but received '{item.Resource.TypeName}'."
                ]));
        }

        FailureMessageList.AddRange(commonRequestValidation.IsRequestResourceIdPopulated(item.ResourceId));

        return GetValidatorResult();
    }
}
