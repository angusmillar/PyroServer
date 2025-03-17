using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.ServiceSettings;
using MediatR;

namespace Abm.Pyro.Domain.ServiceSettingRequest;

public record FhirValidationSettingsUpdateRequest(
    FhirValidationSettings FhirValidationSettings) : IRequest<FhirValidationSettingsUpdateResponse>;

public record FhirValidationSettingsUpdateResponse(
    FhirValidationSettings FhirValidationSettings, 
    IRepositoryEventCollector RepositoryEventCollector, 
    bool CanCommitTransaction) 
    : TransactionResponse(
        RepositoryEventCollector, 
        CanCommitTransaction);