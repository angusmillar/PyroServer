using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class ReadRequestValidator(
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirReadRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirReadRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(item.ResourceName).AllowRead)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        IsValidRequestEndpointResourceType(item);
        IsRequestResourceIdPopulated(item);
        
        return GetValidatorResult();
    }

    private void IsValidRequestEndpointResourceType(FhirReadRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsValidRequestEndpointResourceType(
            item.ResourceName));
    }

    private void IsRequestResourceIdPopulated(FhirReadRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsRequestResourceIdPopulated(
            requestResourceId: item.ResourceId));
    }
    
}