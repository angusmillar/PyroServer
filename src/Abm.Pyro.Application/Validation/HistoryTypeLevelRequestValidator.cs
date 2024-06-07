using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class HistoryTypeLevelRequestValidator(
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirHistoryTypeLevelRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirHistoryTypeLevelRequest item)
    {
        if (!endpointPolicyService.GetEndpointPolicy(item.ResourceName).AllowHistory)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
       
        return GetValidatorResult();
    }
}