using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class ConditionalDeleteRequestValidator(
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirConditionalDeleteRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirConditionalDeleteRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(item.ResourceName).AllowConditionalDelete)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        FailureMessageList.AddRange(commonRequestValidation.IsValidRequestEndpointResourceType(
            requestEndpointResourceName: item.ResourceName));
        
        return GetValidatorResult();
    }
    
}