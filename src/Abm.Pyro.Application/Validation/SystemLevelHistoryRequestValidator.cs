using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class SystemLevelHistoryRequestValidator(
    ITenantService tenantService,
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirSystemLevelHistoryRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirSystemLevelHistoryRequest item)
    {
        if (!endpointPolicyService.GetDefaultEndpointPolicy(tenantService.GetScopedTenantCode()).AllowBaseHistory)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        return GetValidatorResult();
    }
}