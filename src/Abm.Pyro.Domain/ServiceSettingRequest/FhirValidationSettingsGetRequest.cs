using Abm.Pyro.Domain.ServiceSettings;
using MediatR;

namespace Abm.Pyro.Domain.ServiceSettingRequest;

public record FhirValidationSettingsGetRequest() : IRequest<FhirValidationSettingsGetResponse>;

public record FhirValidationSettingsGetResponse(FhirValidationSettings FhirValidationSettings);
