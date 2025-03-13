using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class TypeLevelOperationRequestValidator(
    ITenantService tenantService,
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirTypeLevelOperationRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirTypeLevelOperationRequest item)
    {
        if (!endpointPolicyService.GetDefaultEndpointPolicy(tenantService.GetScopedTenantCode()).AllowResourceTypeOperations.Contains(item.OperationName))
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        return GetValidatorResult();
    }
}