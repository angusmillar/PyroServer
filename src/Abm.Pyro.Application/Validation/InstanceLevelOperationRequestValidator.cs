using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class InstanceLevelOperationRequestValidator(
    ITenantService tenantService,
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirInstanceLevelOperationRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirInstanceLevelOperationRequest item)
    {
        if (!endpointPolicyService.GetDefaultEndpointPolicy(tenantService.GetScopedTenantCode()).AllowResourceInstanceOperations.Contains(item.OperationName))
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        return GetValidatorResult();
    }
}