using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class MetaDataRequestValidator(
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirMetaDataRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirMetaDataRequest item)
    {
        if (!endpointPolicyService.GetDefaultEndpointPolicy().AllowBaseMetadata)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        return GetValidatorResult();
    }
}