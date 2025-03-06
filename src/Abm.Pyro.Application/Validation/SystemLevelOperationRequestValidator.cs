using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class SystemLevelOperationRequestValidator(
    ITenantService tenantService,
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirSystemLevelOperationRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirSystemLevelOperationRequest item)
    {
        if (!endpointPolicyService.GetDefaultEndpointPolicy(tenantService.GetScopedTenantCode()).AllowBaseOperations.Contains(item.OperationName))
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        return GetValidatorResult();
    }
}