using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class SearchRequestValidator(
    ITenantService tenantService,
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirSearchRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirSearchRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(tenantService.GetScopedTenantCode(), item.ResourceName).AllowSearch)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }

        IsValidRequestEndpointResourceType(item);
        
        return GetValidatorResult();
    }

    private void IsValidRequestEndpointResourceType(FhirSearchRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsValidRequestEndpointResourceType(
            item.ResourceName));
    }

}