using Abm.Pyro.Domain.ServiceSettings;
using Abm.Pyro.Domain.Dispatcher;

namespace Abm.Pyro.Domain.ServiceSettingRequest;

public record FhirValidationSettingsGetRequest() : IRequest<FhirValidationSettingsGetResponse>;

public record FhirValidationSettingsGetResponse(FhirValidationSettings FhirValidationSettings);
