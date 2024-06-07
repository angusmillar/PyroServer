using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class UpdateRequestValidator(
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirUpdateRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirUpdateRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(item.ResourceName).AllowUpdate)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        DoResourceNamesMatch(item);
        
        IsRequestResourceIdPopulated(item);
        
        DoResourceIdsMatch(item);
        
        return GetValidatorResult();
    }

    private void DoResourceIdsMatch(FhirUpdateRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.DoResourceIdsMatch(
            item.ResourceId, 
            item.Resource.Id));
    }

    private void IsRequestResourceIdPopulated(FhirUpdateRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsRequestResourceIdPopulated(
            requestResourceId: item.ResourceId));
    }

    private void DoResourceNamesMatch(FhirUpdateRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.DoResourceNamesMatch(
            requestEndpointResourceName: item.ResourceName, 
            requestBodyResourceResourceName: item.Resource.TypeName));
    }
}