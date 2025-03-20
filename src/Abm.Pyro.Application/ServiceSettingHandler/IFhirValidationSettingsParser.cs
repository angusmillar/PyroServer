using Abm.Pyro.Domain.ServiceSettings;
using Hl7.Fhir.Model;

namespace Abm.Pyro.Application.ServiceSettingHandler;

public interface IFhirValidationSettingsParser
{
    ServiceSettingsParserOutcome<FhirValidationSettings> GetSettings(Parameters parameters);

    Parameters GetParametersResource(FhirValidationSettings fhirValidationSettings);
}