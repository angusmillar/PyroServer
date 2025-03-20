using Abm.Pyro.Domain.FhirSupport;
using Abm.Pyro.Domain.ServiceSettings;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.ServiceSettingHandler;

public abstract class ServiceSettingsParserBase(
    IFhirParameterSupport fhirParameterSupport, 
    IOperationOutcomeSupport operationOutcomeSupport)
{
    protected IFhirParameterSupport FhirParameterSupport => fhirParameterSupport;
    protected IOperationOutcomeSupport OperationOutcomeSupport => operationOutcomeSupport;
    protected readonly List<OperationOutcome> OperationOutcomeList = new();
    
    protected Parameters.ParameterComponent? ProfileParameterComponent(
        string parameterName,
        string valueType,
        Parameters parameters)
    {
        Parameters.ParameterComponent? parameterComponent = fhirParameterSupport.GetFirstParameterComponentByName(
            parameterName: parameterName,
            parameterComponent: parameters.Parameter);
        
        if (parameterComponent is null)
        {
            OperationOutcomeList.Add(operationOutcomeSupport.GetError(
                messageList: [$"Missing parameter name: '{parameterName}' with value type: '{valueType}'"]));
        }

        return parameterComponent;
    }

    protected ServiceSettingsParserOutcome<T> GetSuccessOutcome<T>(T serviceSettings) 
        where T : ServiceSettingsBase
    {
        return new ServiceSettingsParserOutcome<T>(
            serviceSettings: serviceSettings,
            operationOutcome: null,
            success: true);
    }
    
    protected ServiceSettingsParserOutcome<T> GetFailedOutcome<T>() 
        where T : ServiceSettingsBase
    {
        return new ServiceSettingsParserOutcome<T>(
            serviceSettings: null,
            operationOutcome: operationOutcomeSupport.MergeOperationOutcomeList(OperationOutcomeList),
            success: false);
    }
}