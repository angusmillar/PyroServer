using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Application.OnStartupService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abm.Pyro.Api.Test.Fixtures;

public class PyroWebApplicationFactory(string sqlConnectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Point the tenant's connection string at the test container
                ["ConnectionStrings:PyroDb"] = sqlConnectionString,

                // Stable base URL for FhirServiceBaseUrlManagementOnStartupService to seed
                ["ServiceBaseUrl:Url"] = "https://localhost",

                // Suppress Spring Cloud Config Server connection attempt
                ["spring:cloud:config:enabled"] = "false",

                // Suppress Steeltoe fail-fast so missing config server does not abort startup
                ["spring:cloud:config:failFast"] = "false",

                // Quiet logging in tests
                ["Serilog:MinimumLevel:Default"] = "Warning",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove only the database version check — it would throw because EF
            // migrations are applied programmatically in the fixture before the
            // factory starts, but the service checks via IDatabasePendingMigrations
            // which queries the EF migration history table and can be unreliable
            // against a freshly migrated container.
            ServiceDescriptor? descriptor = services.FirstOrDefault(d =>
                d.ImplementationType ==
                typeof(AppStartupServiceManager<DatabaseVersionValidationOnStartupService>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }
        });
    }
}
