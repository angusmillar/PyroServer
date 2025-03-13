using Abm.Pyro.Domain.FhirValidate;
using Abm.Pyro.Domain.Support;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Primitives;
using FhirUri = Hl7.Fhir.Model.FhirUri;

namespace Abm.Pyro.Application.FhirValidateService;

public static class FhirValidateSupport
{
    private const string ModeParametersName = "mode";
    private const string ResourceParametersName = "resource";
    private const string ProfileParametersName = "profile";
    
    public static FhirValidateRequest GetRequestFromParameterResource(Parameters parameter)
    {
        return new FhirValidateRequest(
            Mode: GetModeCode(parameter.Parameter),
            Profile: GetProfileUri(parameter.Parameter),
            Resource: GetResource(parameter.Parameter));
    }
    
    public static FhirValidateRequest GetRequestFromQuery(string? queryString)
    {
        var parseHttpQuery = queryString.ParseHttpQuery();
        
        return new FhirValidateRequest(
            Mode: GetModeCodeFromString(GetParameterFromQuery(ModeParametersName, parseHttpQuery)),
            Profile: GetParameterFromQuery(ProfileParametersName, parseHttpQuery),
            Resource: null);
    }

    private static string? GetParameterFromQuery(string parameterName, Dictionary<string, StringValues> parseHttpQuery)
    {
        if (parseHttpQuery.TryGetValue(parameterName, out var value))
        {
            return value.FirstOrDefault();
        }
        return null;
    }

    private static FhirValidateMode? GetModeCode(List<Parameters.ParameterComponent> parameterComponentList)
    {
        Parameters.ParameterComponent?
            modeParameter = GetOptionalParameter(ModeParametersName, parameterComponentList);
        if (modeParameter is not null && modeParameter.Value is Code code && !string.IsNullOrWhiteSpace(code.Value))
        {
            return GetModeCodeFromString(code.Value);
        }

        return null;
    }

    private static FhirValidateMode? GetModeCodeFromString(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }
        switch (code.Trim().ToLower())
        {
            case "create":
                return FhirValidateMode.Create;
            case "update":
                return FhirValidateMode.Update;
            case "delete":
                return FhirValidateMode.Delete;
            case "profile":
                return FhirValidateMode.Profile;
            default:
                return null;
        }
    }

    private static string? GetProfileUri(List<Parameters.ParameterComponent> parameterComponentList)
    {
        Parameters.ParameterComponent? profileParameter =
            GetOptionalParameter(ProfileParametersName, parameterComponentList);
        if (profileParameter?.Value is FhirUri fhirUri)
        {
            return fhirUri.Value;
        }

        return null;
    }

    private static Resource? GetResource(List<Parameters.ParameterComponent> parameterComponentList)
    {
        Parameters.ParameterComponent? resourceParameter = GetOptionalParameter(ResourceParametersName, parameterComponentList);
        
        return resourceParameter?.Resource;
    }
    
    private static Parameters.ParameterComponent? GetOptionalParameter(
        string parameterName,
        List<Parameters.ParameterComponent> parameterList)
    {
        return parameterList.FirstOrDefault(x =>
            x.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
    }
}