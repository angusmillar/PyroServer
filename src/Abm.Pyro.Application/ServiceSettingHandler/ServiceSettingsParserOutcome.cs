using Abm.Pyro.Domain.ServiceSettings;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.ServiceSettingHandler;

public class ServiceSettingsParserOutcome<T>(
    bool success,
    T? serviceSettings,
    OperationOutcome? operationOutcome)
    where T : ServiceSettingsBase
{
    public bool Success { get; set; } = success;

    public T? ServiceSettings { get; } = serviceSettings;

    public OperationOutcome? OperationOutcome { get; } = operationOutcome;
}