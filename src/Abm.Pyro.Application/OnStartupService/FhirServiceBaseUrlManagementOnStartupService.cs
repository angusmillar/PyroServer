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
    //IServiceBaseUrlCache serviceBaseUrlCache,
    IServiceBaseUrlGetPrimaryOnStartup serviceBaseUrlGetPrimaryOnStartup,
    
    IServiceBaseUrlAddByUriOnStartup serviceBaseUrlAddByUriOnStartup,
    IServiceBaseUrlUpdateOnStartup serviceBaseUrlUpdateOnStartup,
    IServiceBaseUrlGetByUriOnStartup serviceBaseUrlGetByUriOnStartup,
    
    //IDatabaseTransactionFactory databaseTransactionFactory,
    ITenantService tenantService)
    : IAppStartupService
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        
        foreach (var tenant in tenantService.GetTenantList())
        {
            tenantService.SetScopedTenant(tenant);
            await ProcessTenantFhirServiceBaseUrl(tenant);
        }
    }

    private async Task ProcessTenantFhirServiceBaseUrl(Domain.Configuration.Tenant tenant)
    {
        ServiceBaseUrl? cachedDatabaseServiceBaseUrl = await serviceBaseUrlGetPrimaryOnStartup.Get();
        Uri appSettingsServiceBaseUrl = new Uri(serviceBaseUrlSettings.Value.Url, tenant.GetUrlCode());

        if (cachedDatabaseServiceBaseUrl is not null && SystemsFhirServiceBaseUrlUnchanged())
        {
            //Normal start-up: The appsettings.json and database primary Service Base URL identical  
            logger.LogInformation("Tenant {Tenant} FHIR Service Base URL: {ServiceBaseUrl}", tenant.DisplayName, cachedDatabaseServiceBaseUrl.Url);
            return;
        }

        //await using IDatabaseTransaction databaseTransaction = databaseTransactionFactory.GetTransaction();
        //await databaseTransaction.BeginTransaction();
        try
        {
            if (cachedDatabaseServiceBaseUrl is null)
            {
                //First time start-up on empty database
                await InitialisePrimaryServiceBaseUrl(appSettingsServiceBaseUrl);
                //await databaseTransaction.Commit();
                //await RePrimePrimaryServiceBaseUrlCache();
                logger.LogInformation("Tenant {Tenant} initialised FHIR Service Base URL for : {ServiceBaseUrl}",
                    tenant.DisplayName,
                    appSettingsServiceBaseUrl.OriginalString.StripHttp());
                return;
            }

            //The Service Base URL has been changed in appsettings.json so we must update the database's primary ServiceBaseURL to align.
            await UpdatePrimaryServiceBaseUrl(cachedDatabaseServiceBaseUrl, appSettingsServiceBaseUrl, tenant);
            //await databaseTransaction.Commit();
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Tenant {Tenant} error validating FHIR Service Base URL", tenant.DisplayName);
            //await databaseTransaction.RollBack();
            throw;
        }

        bool SystemsFhirServiceBaseUrlUnchanged()
        {
            return appSettingsServiceBaseUrl.FhirServiceBaseUrlsAreEqual(
                new Uri($"https://{cachedDatabaseServiceBaseUrl.Url}"));
        }
        
    }

    private async Task InitialisePrimaryServiceBaseUrl(Uri appSettingsServiceBaseUrl)
    {
        await AddServiceBaseUrl(new ServiceBaseUrl(
            serviceBaseUrlId: null,
            url: appSettingsServiceBaseUrl.FhirServiceBaseUrlFormattedString(),
            isPrimary: true));
    }

    private async Task UpdatePrimaryServiceBaseUrl(ServiceBaseUrl cachedDatabaseServiceBaseUrl,
        Uri appSettingsServiceBaseUrl,
        Domain.Configuration.Tenant tenant)
    {
        try
        {
            //Update old database primary ServiceBaseUrl record to Primary = false and remove from cache
            await UpdateOldPrimaryAsNotPrimary(cachedDatabaseServiceBaseUrl);
            //await RemovePrimaryServiceBaseUrlFromCache();

            //Lookup the new primary URL as it may already be in the database as Primary = false
            string appSettingsServiceBaseUrlFormattedString = appSettingsServiceBaseUrl.FhirServiceBaseUrlFormattedString();
            ServiceBaseUrl? existingDatabaseServiceBaseUrl = await serviceBaseUrlGetByUriOnStartup.Get(appSettingsServiceBaseUrlFormattedString);
            if (existingDatabaseServiceBaseUrl is null)
            {
                await AddServiceBaseUrl(new ServiceBaseUrl(
                    serviceBaseUrlId: null,
                    url: appSettingsServiceBaseUrlFormattedString,
                    isPrimary: true));

                //await RePrimePrimaryServiceBaseUrlCache();

                logger.LogInformation("Tenant {Tenant} FHIR Service Base URL updated from {OldServiceBaseUrl} to {NewServiceBaseUrl}",
                    tenant.DisplayName,
                    cachedDatabaseServiceBaseUrl.Url,
                    appSettingsServiceBaseUrlFormattedString
                );
                return;
            }

            await UpdateExistingAsNewPrimaryServiceBaseUrl(existingDatabaseServiceBaseUrl);
            //await RePrimePrimaryServiceBaseUrlCache();

            logger.LogInformation("Tenant {Tenant} FHIR Service Base URL changed from {OldServiceBaseUrl} to {NewServiceBaseUrl}",
                tenant.DisplayName,
                cachedDatabaseServiceBaseUrl.Url,
                existingDatabaseServiceBaseUrl.Url);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Tenant {Tenant} error validating FHIR Service Base URL", tenant.DisplayName);
            throw;
        }
    }

    // private async Task RemovePrimaryServiceBaseUrlFromCache()
    // {
    //     await serviceBaseUrlCache.RemovePrimary();
    // }
    //
    // private async Task RePrimePrimaryServiceBaseUrlCache()
    // {
    //     await serviceBaseUrlCache.GetPrimaryAsync();
    // }

    private async Task AddServiceBaseUrl(ServiceBaseUrl serviceBaseUrl)
    {
        try
        {
            await serviceBaseUrlAddByUriOnStartup.Add(serviceBaseUrl);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Error adding Service Base URL to the database, URL was {ServiceBaseUrl} as Primary:{IsPrimary}",
                serviceBaseUrl.Url,
                serviceBaseUrl.IsPrimary);
            throw;
        }
    }

    private async Task UpdateExistingAsNewPrimaryServiceBaseUrl(ServiceBaseUrl existingDatabaseServiceBaseUrl)
    {
        try
        {
            existingDatabaseServiceBaseUrl.IsPrimary = true;
            await serviceBaseUrlUpdateOnStartup.Update(existingDatabaseServiceBaseUrl);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Error updating existing Service Base URL record to primary, existing URL was {DatabaseServiceBaseUrl} with key {ServiceBaseUrlId}",
                existingDatabaseServiceBaseUrl.Url,
                existingDatabaseServiceBaseUrl.ServiceBaseUrlId);
            throw;
        }
    }

    private async Task UpdateOldPrimaryAsNotPrimary(ServiceBaseUrl cachedDatabaseServiceBaseUrl)
    {
        try
        {
            cachedDatabaseServiceBaseUrl.IsPrimary = false;
            await serviceBaseUrlUpdateOnStartup.Update(cachedDatabaseServiceBaseUrl);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Error updating existing primary Service Base URL record to non-primary, existing URL was {DatabaseServiceBaseUrl} with key {ServiceBaseUrlId}",
                cachedDatabaseServiceBaseUrl.Url,
                cachedDatabaseServiceBaseUrl.ServiceBaseUrlId);
            throw;
        }
    }
}