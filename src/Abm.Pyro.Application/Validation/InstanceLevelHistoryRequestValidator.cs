using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class InstanceLevelHistoryRequestValidator(
    ITenantService tenantService,
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirInstanceLevelHistoryRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirInstanceLevelHistoryRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), item.ResourceName).AllowHistory)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        IsValidRequestEndpointResourceType(item);
        IsRequestResourceIdPopulated(item);
        
       
        return GetValidatorResult();
    }

    
    private void IsValidRequestEndpointResourceType(FhirInstanceLevelHistoryRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsValidRequestEndpointResourceType(
            item.ResourceName));
    }
    
    private void IsRequestResourceIdPopulated(FhirInstanceLevelHistoryRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsRequestResourceIdPopulated(
            requestResourceId: item.ResourceId));
    }

}