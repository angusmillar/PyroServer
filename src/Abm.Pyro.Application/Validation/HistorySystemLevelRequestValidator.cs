using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class HistorySystemLevelRequestValidator(
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirHistorySystemLevelRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirHistorySystemLevelRequest item)
    {
        if (!endpointPolicyService.GetDefaultEndpointPolicy().AllowBaseHistory)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        return GetValidatorResult();
    }
}