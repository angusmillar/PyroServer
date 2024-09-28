using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Domain.Query;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.OnStartupService;

public class DatabaseVersionValidationOnStartupService(
    ILogger<FhirServiceBaseUrlManagementOnStartupService> logger,
    IDatabasePendingMigrations databasePendingMigrations) : IAppStartupService
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        string[] pendingDbMigrationsList = await databasePendingMigrations.Get();
        if (pendingDbMigrationsList.Length == 0)
        {
            return;
        }
        
        logger.LogCritical("The following database migrations are missing: ");
        foreach (var migrationName in pendingDbMigrationsList)
        {
            logger.LogCritical("Missing Migration: {MigrationName}", migrationName);
            throw new ApplicationException("The applications database is missing the required migrations. " +
                                           "Please refer to the application's logs for details."); 
        }
    }
}