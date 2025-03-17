using System.Text.Json;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.ServiceSettingRequest;
using Abm.Pyro.Domain.ServiceSettings;
using Abm.Pyro.Domain.Support;
using MediatR;

namespace Abm.Pyro.Application.ServiceSettingHandler;

public class ServiceSettingHandler(
    IServiceConfigurationGetCurrentByType serviceConfigurationGetCurrentByType, 
    IServiceConfigurationUpdate serviceConfigurationUpdate, 
    IServiceConfigurationAdd serviceConfigurationAdd, 
    IDateTimeProvider dateTimeProvider,
    IServiceSettingsCache serviceSettingsCache,
    IRepositoryEventCollector repositoryEventCollector) 
    : IRequestHandler<FhirValidationSettingsUpdateRequest, FhirValidationSettingsUpdateResponse>,
        IRequestHandler<FhirValidationSettingsGetRequest, FhirValidationSettingsGetResponse>
{
    

    public async Task<FhirValidationSettingsGetResponse> Handle(FhirValidationSettingsGetRequest request, CancellationToken cancellationToken)
    {
        FhirValidationSettings fhirValidationSettings = await serviceSettingsCache.GetFhirValidationSettings();
        
        return new FhirValidationSettingsGetResponse(FhirValidationSettings: fhirValidationSettings);
    }

    public async Task<FhirValidationSettingsUpdateResponse> Handle(FhirValidationSettingsUpdateRequest fhirValidationSettingsUpdateRequest, CancellationToken cancellationToken)
    {
        DateTime now = dateTimeProvider.Now.UtcDateTime;
        
        ServiceSetting fhirValidationServiceSettings = await serviceConfigurationGetCurrentByType.Get(typeId: ServiceSettingTypeId.FhirValidation);
        
        fhirValidationServiceSettings.IsCurrent = false;
        
        await serviceConfigurationUpdate.Update(fhirValidationServiceSettings);

        int newVersionId = fhirValidationServiceSettings.VersionId + 1;
        
        fhirValidationSettingsUpdateRequest.FhirValidationSettings.VersionId = newVersionId.ToString();
        fhirValidationSettingsUpdateRequest.FhirValidationSettings.LastUpdated = now;
        
        ServiceSetting serviceSettingFromRequest = new ServiceSetting(
            serviceSettingId: null, 
            versionId: newVersionId, 
            isCurrent: true, 
            typeId: ServiceSettingTypeId.FhirValidation,
            json: JsonSerializer.Serialize(fhirValidationSettingsUpdateRequest.FhirValidationSettings),
            lastUpdatedUtc: now);

        await serviceConfigurationAdd.Add(serviceSettingFromRequest);
        
        await serviceSettingsCache.Remove(serviceSettingType: ServiceSettingTypeId.FhirValidation);
        
        return new FhirValidationSettingsUpdateResponse(
            FhirValidationSettings: fhirValidationSettingsUpdateRequest.FhirValidationSettings,
            RepositoryEventCollector: repositoryEventCollector,
            CanCommitTransaction: true);
    }
}