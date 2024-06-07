using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class VersionReadRequestValidator(
    ICommonRequestValidation commonRequestValidation,
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirVersionReadRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirVersionReadRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(item.ResourceName).AllowVersionRead)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        IsValidRequestEndpointResourceType(item);
        IsRequestResourceIdPopulated(item);
        IsRequestHistoryIdPopulated(item);
       
        return GetValidatorResult();
    }

    
    private void IsValidRequestEndpointResourceType(FhirResourceNameResourceIdRequestBase item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsValidRequestEndpointResourceType(
            item.ResourceName));
    }
    
    private void IsRequestResourceIdPopulated(FhirResourceNameResourceIdRequestBase item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsRequestResourceIdPopulated(
            requestResourceId: item.ResourceId));
    }
    
    private void IsRequestHistoryIdPopulated(FhirVersionReadRequest item)
    {
        FailureMessageList.AddRange(commonRequestValidation.IsRequestResourceIdPopulated(
            requestResourceId: item.HistoryId));
    }

}