using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.Validation;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using FhirUri = Hl7.Fhir.Model.FhirUri;

namespace Abm.Pyro.Application.FhirValidateService;

public class FhirValidateOperationRequestValidator(IOperationOutcomeSupport operationOutcomeSupport) 
    : ValidatorBase<FhirSystemLevelOperationRequest>(operationOutcomeSupport)
{

    private readonly (string Mode, string Resource, string Profile) _parameterName = ("mode", "resource", "profile");

    private static readonly string[] AllowedModeCodes = [ "create", "update", "delete", "profile" ];
    public override ValidatorResult Validate(FhirSystemLevelOperationRequest request)
    {
        if (request.Resource is not Parameters parameters)
        {
            FailureMessageList.Add($"The FHIR ${FhirValidateOperationService.OperationName} operation must be provided a resource type of: {ResourceType.Parameters.GetLiteral()}, encountered type of: {request.Resource.TypeName}. ");
            return GetValidatorResult();
        }
        
        Parameters.ParameterComponent? modeParameter = GetOptionalParameter(_parameterName.Mode, parameters.Parameter);
        Parameters.ParameterComponent? resourceParameter = GetOptionalParameter(_parameterName.Resource, parameters.Parameter);
        Parameters.ParameterComponent? profileParameter = GetOptionalParameter(_parameterName.Profile, parameters.Parameter);

        if (HasFailures)
        {
            return GetValidatorResult();
        }

        string modeCode = GetModeCode(modeParameter);

        if (!string.IsNullOrWhiteSpace(modeCode) && !AllowedModeCodes.Contains(modeCode))
        {
            FailureMessageList.Add($"The FHIR ${FhirValidateOperationService.OperationName} operation parameter named " +
                                   $"'{_parameterName.Mode}', must be one of the following: " +
                                   $"'{string.Join(',', AllowedModeCodes)}', the provided code was : {modeCode}. ");
            return GetValidatorResult();
        }
        
        if (resourceParameter?.Resource is null && !IsModeDelete(modeCode))
        {
            
            FailureMessageList.Add($"The FHIR ${FhirValidateOperationService.OperationName} operation parameter named " +
                                   $"'{_parameterName.Resource}', must be provided where '{_parameterName.Mode}' is " +
                                   $"{(string.IsNullOrWhiteSpace(modeCode) ? "not provided" : $"'{modeCode}'")}. ");
            return GetValidatorResult();
        }
         
        if (profileParameter is not null && profileParameter.Value is not FhirUri)
        {
            FailureMessageList.Add($"The FHIR ${FhirValidateOperationService.OperationName} operation parameter named " +
                                   $"'{_parameterName.Profile}', must be of FHIR datatype URI, found type " +
                                   $"{profileParameter.Value.GetType().Name} '. ");
            return GetValidatorResult();
        }
        
        return GetValidatorResult();
    }

    private static bool IsModeDelete(string modeCode)
    {
        return modeCode.Equals("delete", StringComparison.OrdinalIgnoreCase);
    }
    
    private static string GetModeCode(Parameters.ParameterComponent? modeParameter)
    {
        if (modeParameter is not null && modeParameter.Value is Code code && !string.IsNullOrWhiteSpace(code.Value))
        {
            return code.Value.Trim();
        }
        return string.Empty;
    }


    private Parameters.ParameterComponent? GetOptionalParameter(string parameterName, List<Parameters.ParameterComponent> parameterList)
    {
        List<Parameters.ParameterComponent> targetParameterList = parameterList.Where(x => x.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (targetParameterList.Count > 1)
        {
            FailureMessageList.Add($"The FHIR ${FhirValidateOperationService.OperationName} operation must have (0..1) parameters named '{parameterName}', found : {targetParameterList.Count}. ");
        }

        return targetParameterList.FirstOrDefault();
    }
}