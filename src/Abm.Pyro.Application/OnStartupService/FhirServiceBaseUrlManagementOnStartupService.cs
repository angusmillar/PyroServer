using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Application.Tenant;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Application.OnStartupService;

public class FhirServiceBaseUrlManagementOnStartupService(
    ILogger<FhirServiceBaseUrlManagementOnStartupService> logger,
    IOptions<ServiceBaseUrlSettings> serviceBaseUrlSettings,
    IServiceBaseUrlOnStartupRepository serviceBaseUrlOnStartupRepository,
    ITenantService tenantService)
    : IAppStartupService
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        foreach (var tenant in tenantService.GetTenantList())
        {
            serviceBaseUrlOnStartupRepository.StartUnitOfWork(tenant);

            await ProcessTenantFhirServiceBaseUrl(tenant);

            await serviceBaseUrlOnStartupRepository.DisposeDbContextAsync();
        }
    }

    private async Task ProcessTenantFhirServiceBaseUrl(Domain.Configuration.Tenant tenant)
    {
        ServiceBaseUrl? cachedDatabaseServiceBaseUrl = await serviceBaseUrlOnStartupRepository.Get();
        Uri appSettingsServiceBaseUrl = new Uri(serviceBaseUrlSettings.Value.Url, tenant.GetUrlCode());

        if (cachedDatabaseServiceBaseUrl is not null && SystemsFhirServiceBaseUrlUnchanged())
        {
            //Normal start-up: The appsettings.json and database primary Service Base URL identical  
            logger.LogInformation("Tenant {Tenant} FHIR Service Base URL: {ServiceBaseUrl}", tenant.DisplayName,
                cachedDatabaseServiceBaseUrl.Url);
            return;
        }

        try
        {
            if (cachedDatabaseServiceBaseUrl is null)
            {
                //First time start-up on empty database
                InitialisePrimaryServiceBaseUrl(appSettingsServiceBaseUrl);

                await serviceBaseUrlOnStartupRepository.SaveChangesAsync();

                logger.LogInformation("Tenant {Tenant} initialised FHIR Service Base URL for : {ServiceBaseUrl}",
                    tenant.DisplayName,
                    appSettingsServiceBaseUrl.OriginalString.StripHttp());

                return;
            }

            //The Service Base URL has been changed in appsettings.json so we must update the database's primary ServiceBaseURL to align.
            await UpdatePrimaryServiceBaseUrl(cachedDatabaseServiceBaseUrl, appSettingsServiceBaseUrl, tenant);

            await serviceBaseUrlOnStartupRepository.SaveChangesAsync();
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Tenant {Tenant} error validating FHIR Service Base URL", tenant.DisplayName);
            throw;
        }

        bool SystemsFhirServiceBaseUrlUnchanged()
        {
            return appSettingsServiceBaseUrl.FhirServiceBaseUrlsAreEqual(
                new Uri($"https://{cachedDatabaseServiceBaseUrl.Url}"));
        }
    }

    private void InitialisePrimaryServiceBaseUrl(Uri appSettingsServiceBaseUrl)
    {
        serviceBaseUrlOnStartupRepository.Add(new ServiceBaseUrl(
            serviceBaseUrlId: null,
            url: appSettingsServiceBaseUrl.FhirServiceBaseUrlFormattedString(),
            isPrimary: true));
    }

    private async Task UpdatePrimaryServiceBaseUrl(ServiceBaseUrl cachedDatabaseServiceBaseUrl,
        Uri appSettingsServiceBaseUrl,
        Domain.Configuration.Tenant tenant)
    {
        //Update old database primary ServiceBaseUrl record to Primary = false and remove from cache
        UpdateOldPrimaryAsNotPrimary(cachedDatabaseServiceBaseUrl);

        //Lookup the new primary URL as it may already be in the database as Primary = false
        string appSettingsServiceBaseUrlFormattedString =
            appSettingsServiceBaseUrl.FhirServiceBaseUrlFormattedString();
        ServiceBaseUrl? existingDatabaseServiceBaseUrl =
            await serviceBaseUrlOnStartupRepository.Get(appSettingsServiceBaseUrlFormattedString);
        if (existingDatabaseServiceBaseUrl is null)
        {
            serviceBaseUrlOnStartupRepository.Add(new ServiceBaseUrl(
                serviceBaseUrlId: null,
                url: appSettingsServiceBaseUrlFormattedString,
                isPrimary: true));

            logger.LogInformation(
                "Tenant {Tenant} FHIR Service Base URL updated from {OldServiceBaseUrl} to {NewServiceBaseUrl}",
                tenant.DisplayName,
                cachedDatabaseServiceBaseUrl.Url,
                appSettingsServiceBaseUrlFormattedString
            );

            return;
        }

        UpdateExistingAsNewPrimaryServiceBaseUrl(existingDatabaseServiceBaseUrl);

        logger.LogInformation(
            "Tenant {Tenant} FHIR Service Base URL changed from {OldServiceBaseUrl} to {NewServiceBaseUrl}",
            tenant.DisplayName,
            cachedDatabaseServiceBaseUrl.Url,
            existingDatabaseServiceBaseUrl.Url);
    }

    private void UpdateExistingAsNewPrimaryServiceBaseUrl(ServiceBaseUrl existingDatabaseServiceBaseUrl)
    {
        existingDatabaseServiceBaseUrl.IsPrimary = true;
        serviceBaseUrlOnStartupRepository.Update(existingDatabaseServiceBaseUrl);
    }

    private void UpdateOldPrimaryAsNotPrimary(ServiceBaseUrl cachedDatabaseServiceBaseUrl)
    {
        cachedDatabaseServiceBaseUrl.IsPrimary = false;
        serviceBaseUrlOnStartupRepository.Update(cachedDatabaseServiceBaseUrl);
    }
}