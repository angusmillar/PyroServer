using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class MetaDataRequestValidator(
    ITenantService tenantService,
    IOperationOutcomeSupport operationOutcomeSupport,
    IEndpointPolicyService endpointPolicyService) 
    : ValidatorBase<FhirMetaDataRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirMetaDataRequest item)
    {
        if (!endpointPolicyService.GetDefaultEndpointPolicy(tenantService.GetScopedTenantCode()).AllowBaseMetadata)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        return GetValidatorResult();
    }
}