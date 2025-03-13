using Abm.Pyro.Application.FhirValidateService;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.FhirValidate;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;

namespace Abm.Pyro.Application.Validation;

public class FhirInstanceLevelValidateOperationRequestValidator(IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirInstanceLevelOperationRequest>(operationOutcomeSupport)
{
    private const FhirOperationLevel FhirOperationLevel = Domain.Enums.FhirOperationLevel.Instance; 
    
    private readonly FhirValidateMode[] AllowedFhirValidateModeList =
    [
        FhirValidateMode.Update, 
        FhirValidateMode.Delete, 
    ];
    public override ValidatorResult Validate(FhirInstanceLevelOperationRequest request)
    {
        //If the endpoint's ResourceName equals the ResourceType provided in the body of the request, then we expect
        //the parameters wil be in the request URL query string, if any at all 
        if (request.ResourceName.Equals(request.Resource.TypeName))
        {
            FhirValidateRequest fhirValidateRequestFromQuery = FhirValidateSupport.GetRequestFromQuery(request.QueryString);
            if (!string.IsNullOrWhiteSpace(fhirValidateRequestFromQuery.Profile))
            {
                if (!Uri.IsWellFormedUriString(fhirValidateRequestFromQuery.Profile, UriKind.Absolute))
                {
                    FailureMessageList.Add(
                        $"The FHIR ${FhirValidateOperationService.OperationName} operation performed at the {FhirOperationLevel} level was " +
                        $"found to have an invalid 'profile' parameter. Expected a valid URI, found {fhirValidateRequestFromQuery.Profile}. ");
                    
                    return GetValidatorResult();
                }
            }

            if (!IsValidModeCode(fhirValidateRequestFromQuery.Mode))
            {
                return GetValidatorResult();    
            }
            
            return GetValidatorResult();
        }
        
        //Otherwise the request's body resource must be a Parameters FHIR Resource type with the validate operation inputs 
        if (request.Resource is not Parameters parameters)
        {
            FailureMessageList.Add(
                $"The FHIR ${FhirValidateOperationService.OperationName} operation performed at the {FhirOperationLevel} " +
                $"level must be provided a {ResourceType.Parameters.GetLiteral()} resource in the request body, or the " +
                $"request body resource must equal the endpoint's resource type. Endpoint resource type was : " +
                $"{request.ResourceName}, where the request body resource type was : {request.Resource.TypeName}. ");
            
            return GetValidatorResult();
        }
        
        var fhirValidateRequestFromParameterResource = FhirValidateSupport.GetRequestFromParameterResource(parameters);
        
        if (!IsValidModeCode(fhirValidateRequestFromParameterResource.Mode))
        {
            return GetValidatorResult();    
        }
        
        
        if (fhirValidateRequestFromParameterResource.Mode is not FhirValidateMode.Delete && fhirValidateRequestFromParameterResource.Resource is null)
        {
            FailureMessageList.Add(
                $"The FHIR ${FhirValidateOperationService.OperationName} operation performed at the {FhirOperationLevel} level must " +
                $"be provided a FHIR resource to validate in a {ResourceType.Parameters.GetLiteral()} resource where the " +
                $"'mode' code is not {FhirValidateMode.Delete.ToString().ToLower()}. ");
            
            return GetValidatorResult();
        }
        
        return GetValidatorResult();
    }

    private bool IsValidModeCode(FhirValidateMode? fhirValidateMode)
    {
        if (fhirValidateMode.HasValue && !AllowedFhirValidateModeList.Contains(fhirValidateMode.Value))
        {
            FailureMessageList.Add(
                $"The FHIR ${FhirValidateOperationService.OperationName} operation performed at the {FhirOperationLevel} level must " +
                $"only have an empty 'mode' parameter or one of following codes {string.Join(',', AllowedFhirValidateModeList)}. " +
                $"Found the 'mode' coded of : {fhirValidateMode}. ");
            
            return false;
        }

        return true;
    }
}