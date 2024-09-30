using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Application.Tenant;
using Abm.Pyro.Domain.Query;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.OnStartupService;

public class DatabaseVersionValidationOnStartupService(
    ILogger<FhirServiceBaseUrlManagementOnStartupService> logger,
    IDatabasePendingMigrations databasePendingMigrations,
    ITenantService tenantService) : IAppStartupService
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        foreach (var tenant in tenantService.GetTenantList())
        {
            tenantService.SetScopedTenant(tenant);
            await ValidateDataBaseVersionForTenant(tenant);
        }
    }

    private async Task ValidateDataBaseVersionForTenant(Domain.Configuration.Tenant tenant)
    {
        string[] pendingDbMigrationsList = await databasePendingMigrations.Get();
        if (pendingDbMigrationsList.Length == 0)
        {
            logger.LogInformation("Tenant {Tenant} database version valid", 
                tenant.DisplayName);
            return;
        }
        
        logger.LogCritical("The following database migrations are missing for the Tenant: {TenantCode}:{TenantName}", 
            tenant.Code, 
            tenant.DisplayName);
            
        foreach (var migrationName in pendingDbMigrationsList)
        {
            logger.LogCritical("Tenant {TenantCode} Missing Migration: {MigrationName}", 
                tenant.Code, 
                migrationName);
        }
        
        throw new ApplicationException($"The applications database is missing the required migrations. " +
                                       "Please refer to the application's logs for details."); 
    }
}