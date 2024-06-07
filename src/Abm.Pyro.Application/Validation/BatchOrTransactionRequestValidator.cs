using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.FhirRequest;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;

namespace Abm.Pyro.Application.Validation;

public class BatchOrTransactionRequestValidator(
    IEndpointPolicyService endpointPolicyService,
    IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirBatchOrTransactionRequest>(operationOutcomeSupport)
{
    public override ValidatorResult Validate(FhirBatchOrTransactionRequest item)
    {
        if (!item.Resource.TypeName.Equals(ResourceType.Bundle.GetLiteral()))
        {
            FailureMessageList.Add($"The [Base] endpoint can only accept the resource type of: {ResourceType.Bundle.GetLiteral()}, encountered type of: {item.Resource.TypeName}. ");
            return GetValidatorResult();
        }

        if (item.Resource is not Bundle bundle)
        {
            throw new InvalidCastException(nameof(item.Resource));
        }

        if (bundle.Type is null)
        {
            FailureMessageList.Add($"The base endpoint can only accept {ResourceType.Bundle.GetLiteral()} resources with a bundle.type equal " +
                                   $"to {Bundle.BundleType.Batch.GetLiteral()} or {Bundle.BundleType.Transaction.GetLiteral()}. " +
                                   $"Encountered an empty bundle.type");
            
            return GetValidatorResult();
        }

        Bundle.BundleType[] allowedBundleTypes = [Bundle.BundleType.Batch, Bundle.BundleType.Transaction];
        if (bundle.Type != null && !allowedBundleTypes.Contains(bundle.Type.Value))
        {
            FailureMessageList.Add($"The base endpoint can only accept {ResourceType.Bundle.GetLiteral()} resources with a bundle.type equal " +
                                   $"to {Bundle.BundleType.Batch.GetLiteral()} or {Bundle.BundleType.Transaction.GetLiteral()}. " +
                                   $"Encountered the type of {bundle.Type.GetLiteral()}");
            
            return GetValidatorResult();
        }
        
        if (bundle.Type == Bundle.BundleType.Batch && !endpointPolicyService.GetDefaultEndpointPolicy().AllowBaseBatch)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        if (bundle.Type == Bundle.BundleType.Transaction && !endpointPolicyService.GetDefaultEndpointPolicy().AllowBaseTransaction)
        {
            return GetFailedEndpointPolicyValidatorResult();    
        }
        
        return GetValidatorResult();
    }
    
}