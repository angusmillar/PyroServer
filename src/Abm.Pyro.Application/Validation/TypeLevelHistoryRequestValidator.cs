using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class TypeLevelHistoryRequestValidator(
    ITenantService tenantService,
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirTypeLevelHistoryRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirTypeLevelHistoryRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), item.ResourceName).AllowHistory)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
       
        return GetValidatorResult();
    }
}