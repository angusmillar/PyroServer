using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class SearchRequestValidator(
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirSearchRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirSearchRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(item.ResourceName).AllowSearch)
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