using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class HistoryInstanceLevelRequestValidator(
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirHistoryInstanceLevelRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirHistoryInstanceLevelRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(item.ResourceName).AllowHistory)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        IsValidRequestEndpointResourceType(item);
        IsRequestResourceIdPopulated(item);
        
       
        return GetValidatorResult();
    }

    
    private void IsValidRequestEndpointResourceType(FhirHistoryInstanceLevelRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsValidRequestEndpointResourceType(
            item.ResourceName));
    }
    
    private void IsRequestResourceIdPopulated(FhirHistoryInstanceLevelRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsRequestResourceIdPopulated(
            requestResourceId: item.ResourceId));
    }

}