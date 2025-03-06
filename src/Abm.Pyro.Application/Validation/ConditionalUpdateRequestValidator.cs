using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class ConditionalUpdateRequestValidator(
    ITenantService tenantService,
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirConditionalUpdateRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirConditionalUpdateRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), item.ResourceName).AllowConditionalUpdate)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        FailureMessageList.AddRange(commonRequestValidation.DoResourceNamesMatch(
            requestEndpointResourceName: item.ResourceName, 
            requestBodyResourceResourceName: item.Resource.TypeName));
        
        return GetValidatorResult();
    }
    
}