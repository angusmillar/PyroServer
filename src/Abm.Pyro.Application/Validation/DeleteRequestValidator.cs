using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class DeleteRequestValidator(
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirDeleteRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirDeleteRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(item.ResourceName).AllowDelete)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        IsValidRequestEndpointResourceType(item);
        IsRequestResourceIdPopulated(item);
        
        return GetValidatorResult();
    }

    private void IsValidRequestEndpointResourceType(FhirDeleteRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsValidRequestEndpointResourceType(
            item.ResourceName));
    }

    private void IsRequestResourceIdPopulated(FhirDeleteRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsRequestResourceIdPopulated(
            requestResourceId: item.ResourceId));
    }
    
}