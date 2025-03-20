using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.ServiceSettings;
using Abm.Pyro.Domain.Support;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.ServiceSettingHandler;

public class FhirValidationSettingsParser(
    IFhirParameterSupport fhirParameterSupport, 
    IOperationOutcomeSupport operationOutcomeSupport,
    IDateTimeProvider dateTimeProvider) 
    : ServiceSettingsParserBase(
        fhirParameterSupport, 
        operationOutcomeSupport), IFhirValidationSettingsParser
{
    private const string ProfilePackageServiceUrlName = "ProfilePackageServiceUrl";
    private const string TerminologyServiceUrlName = "TerminologyServiceUrl";
    private const string ValidateOnCreateName = "ValidateOnCreate";
    private const string ValidateOnUpdateName = "validateOnUpdate";
    public ServiceSettingsParserOutcome<FhirValidationSettings> GetSettings(Parameters parameters)
    {
        
        var profileParameterComponent = ProfileParameterComponent(
            parameterName: ProfilePackageServiceUrlName,
            valueType: "valueUrl",
            parameters: parameters);
        
        var terminologyServiceParameterComponent = ProfileParameterComponent(
            parameterName: TerminologyServiceUrlName,
            valueType: "valueUrl",
            parameters: parameters);
        
        var validateOnCreateParameterComponent = ProfileParameterComponent(
            parameterName: ValidateOnCreateName,
            valueType: "valueBoolean",
            parameters: parameters);
        
        var validateOnUpdateParameterComponent = ProfileParameterComponent(
            parameterName: ValidateOnUpdateName,
            valueType: "valueBoolean",
            parameters: parameters);
        
        if (OperationOutcomeList.Count > 0)
        {
            return GetFailedOutcome<FhirValidationSettings>();
        }
        
        ArgumentNullException.ThrowIfNull(profileParameterComponent);
        ArgumentNullException.ThrowIfNull(terminologyServiceParameterComponent);
        ArgumentNullException.ThrowIfNull(validateOnCreateParameterComponent);
        ArgumentNullException.ThrowIfNull(validateOnUpdateParameterComponent);
        
        Uri? profilePackageServiceUrl = FhirParameterSupport.GetParameterFhirUrlValue(profileParameterComponent);
        if (profilePackageServiceUrl is null)
        {
            OperationOutcomeList.Add(OperationOutcomeSupport.GetError(
                messageList: [$"The parameter named: '{ProfilePackageServiceUrlName}' is missing its value of type: 'valueUrl'"]));
            return GetFailedOutcome<FhirValidationSettings>();
        }
        
        Uri? terminologyServiceUrl = FhirParameterSupport.GetParameterFhirUrlValue(terminologyServiceParameterComponent);
        if (terminologyServiceUrl is null)
        {
            OperationOutcomeList.Add(OperationOutcomeSupport.GetError(
                messageList: [$"The parameter named: '{TerminologyServiceUrlName}' is missing its value of type: 'valueUrl'"]));
            return GetFailedOutcome<FhirValidationSettings>();
        }
        
        bool? validateOnCreate = FhirParameterSupport.GetParameterFhirBoolValue(validateOnCreateParameterComponent);
        if (validateOnCreate is null)
        {
            OperationOutcomeList.Add(OperationOutcomeSupport.GetError(
                messageList: [$"The parameter named: '{ValidateOnCreateName}' is missing its value of type: 'valueBoolean'"]));
            return GetFailedOutcome<FhirValidationSettings>();
        }
        
        bool? validateOnUpdate = FhirParameterSupport.GetParameterFhirBoolValue(validateOnUpdateParameterComponent);
        if (validateOnUpdate is null)
        {
            OperationOutcomeList.Add(OperationOutcomeSupport.GetError(
                messageList: [$"The parameter named: '{ValidateOnUpdateName}' is missing its value of type: 'valueBoolean'"]));
            return GetFailedOutcome<FhirValidationSettings>();
        }
        
        var fhirValidationSettings = new FhirValidationSettings(
            versionId: parameters.Meta.VersionId,
            lastUpdated: dateTimeProvider.Now.DateTime,
            profilePackageServiceUrl: profilePackageServiceUrl,
            terminologyServiceUrl: terminologyServiceUrl,
            validateOnCreate: validateOnCreate.Value, //defaults to false if null
            validateOnUpdate: validateOnUpdate.Value); //defaults to false if null

        return GetSuccessOutcome(fhirValidationSettings);
        
    }
    
    public Parameters GetParametersResource(FhirValidationSettings fhirValidationSettings)
    {

        var parameterList = new List<Parameters.ParameterComponent>();

        if (!string.IsNullOrWhiteSpace(fhirValidationSettings.ProfilePackageServiceUrl?.OriginalString))
        {
            parameterList.Add(new Parameters.ParameterComponent()
            {
                Name = ProfilePackageServiceUrlName,
                Value = new FhirUrl(value: fhirValidationSettings.ProfilePackageServiceUrl.OriginalString),
            });
        }
        
        if (!string.IsNullOrWhiteSpace(fhirValidationSettings.TerminologyServiceUrl?.OriginalString))
        {
            parameterList.Add(new Parameters.ParameterComponent()
            {
                Name = TerminologyServiceUrlName,
                Value = new FhirUrl(value: fhirValidationSettings.TerminologyServiceUrl.OriginalString),
            });
        }
        
        parameterList.Add(new Parameters.ParameterComponent()
        {
            Name = ValidateOnCreateName,
            Value = new FhirBoolean(value: fhirValidationSettings.ValidateOnUpdate),
        });
        
        parameterList.Add(new Parameters.ParameterComponent()
        {
            Name = ValidateOnUpdateName,
            Value = new FhirBoolean(value: fhirValidationSettings.ValidateOnCreate),
        });
        
        return new Parameters()
        {
            Meta = new Meta()
            {
                LastUpdated = fhirValidationSettings.LastUpdated,
                VersionId = fhirValidationSettings.VersionId
            },
            Parameter = parameterList
        };
        
    }
}