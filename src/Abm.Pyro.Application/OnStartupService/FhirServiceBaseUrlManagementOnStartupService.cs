using Abm.Pyro.Application.HostedServiceSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Abm.Pyro.Domain.Cache;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Domain.Support;

namespace Abm.Pyro.Application.OnStartupService;

public class FhirServiceBaseUrlManagementOnStartupService(
    ILogger<FhirServiceBaseUrlManagementOnStartupService> logger,
    IOptions<ServiceBaseUrlSettings> serviceBaseUrlSettings,
    IServiceBaseUrlCache serviceBaseUrlCache,
    IServiceBaseUrlAddByUri serviceBaseUrlAddByUri,
    IServiceBaseUrlUpdate serviceBaseUrlUpdate,
    IServiceBaseUrlGetByUri serviceBaseUrlGetByUri,
    IDatabaseTransactionFactory databaseTransactionFactory)
    : IAppStartupService
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        ServiceBaseUrl? cachedDatabaseServiceBaseUrl = await serviceBaseUrlCache.GetPrimaryAsync();
        Uri appSettingsServiceBaseUrl = serviceBaseUrlSettings.Value.Url;
        
        if (cachedDatabaseServiceBaseUrl is not null && SystemsFhirServiceBaseUrlUnchanged())
        {
            //Normal start-up: The appsettings.json and database primary Service Base URL identical  
            logger.LogInformation("FHIR Service Base URL: {ServiceBaseUrl}", cachedDatabaseServiceBaseUrl.Url);
            return;
        }
        
        await using IDatabaseTransaction databaseTransaction = databaseTransactionFactory.GetTransaction();
        await databaseTransaction.BeginTransaction();
        try
        {
            if (cachedDatabaseServiceBaseUrl is null)
            {
                //First time start-up on empty database
                await InitialisePrimaryServiceBaseUrl(appSettingsServiceBaseUrl);
                await databaseTransaction.Commit();
                await RePrimePrimaryServiceBaseUrlCache();
                logger.LogInformation("Initialise FHIR Service Base URL: {ServiceBaseUrl}", appSettingsServiceBaseUrl.OriginalString.StripHttp());
                return;
            }
            
            //The Service Base URL has been changed in appsettings.json so we must update the database's primary ServiceBaseURL to align.
            await UpdatePrimaryServiceBaseUrl(cachedDatabaseServiceBaseUrl, appSettingsServiceBaseUrl);
            await databaseTransaction.Commit();
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Error validating FHIR Service Base URL");
            await databaseTransaction.RollBack();
            throw;
        }

        bool SystemsFhirServiceBaseUrlUnchanged()
        {
            return appSettingsServiceBaseUrl.FhirServiceBaseUrlsAreEqual(new Uri($"https://{cachedDatabaseServiceBaseUrl.Url}"));
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
        Uri appSettingsServiceBaseUrl)
    {
        try
        {
            //Update old database primary ServiceBaseUrl record to Primary = false and remove from cache
            await UpdateOldPrimaryAsNotPrimary(cachedDatabaseServiceBaseUrl);
            await RemovePrimaryServiceBaseUrlFromCache();

            //Lookup the new primary URL as it may already be in the database as Primary = false
            string appSettingsServiceBaseUrlFormattedString = appSettingsServiceBaseUrl.FhirServiceBaseUrlFormattedString();
            ServiceBaseUrl? existingDatabaseServiceBaseUrl = await serviceBaseUrlGetByUri.Get(appSettingsServiceBaseUrlFormattedString);
            if (existingDatabaseServiceBaseUrl is null)
            {
                await AddServiceBaseUrl(new ServiceBaseUrl(
                    serviceBaseUrlId: null,
                    url: appSettingsServiceBaseUrlFormattedString,
                    isPrimary: true));

                await RePrimePrimaryServiceBaseUrlCache();

                logger.LogInformation("FHIR Service Base URL updated from {OldServiceBaseUrl} to {NewServiceBaseUrl}",
                    cachedDatabaseServiceBaseUrl.Url,
                    appSettingsServiceBaseUrlFormattedString
                );
                return;
            }

            await UpdateExistingAsNewPrimaryServiceBaseUrl(existingDatabaseServiceBaseUrl);
            await RePrimePrimaryServiceBaseUrlCache();

            logger.LogInformation("FHIR Service Base URL changed from {OldServiceBaseUrl} to {NewServiceBaseUrl}",
                cachedDatabaseServiceBaseUrl.Url,
                existingDatabaseServiceBaseUrl.Url);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Error validating FHIR Service Base URL");
            throw;
        }
    }

    private async Task RemovePrimaryServiceBaseUrlFromCache()
    {
        await serviceBaseUrlCache.RemovePrimary();
    }

    private async Task RePrimePrimaryServiceBaseUrlCache()
    {
        await serviceBaseUrlCache.GetPrimaryAsync();
    }

    private async Task AddServiceBaseUrl(ServiceBaseUrl serviceBaseUrl)
    {
        try
        {
            await serviceBaseUrlAddByUri.Add(serviceBaseUrl);
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
            await serviceBaseUrlUpdate.Update(existingDatabaseServiceBaseUrl);
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
            await serviceBaseUrlUpdate.Update(cachedDatabaseServiceBaseUrl);
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