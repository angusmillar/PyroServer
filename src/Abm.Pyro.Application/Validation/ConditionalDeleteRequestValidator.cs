using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class ConditionalDeleteRequestValidator(
    ITenantService tenantService,
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirConditionalDeleteRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirConditionalDeleteRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), item.ResourceName).AllowConditionalDelete)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        FailureMessageList.AddRange(commonRequestValidation.IsValidRequestEndpointResourceType(
            requestEndpointResourceName: item.ResourceName));
        
        return GetValidatorResult();
    }
    
}