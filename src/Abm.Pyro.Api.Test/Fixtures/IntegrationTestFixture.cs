using Abm.Pyro.Repository;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.MsSql;

namespace Abm.Pyro.Api.Test.Fixtures;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private PyroWebApplicationFactory _factory = default!;
    private Respawner _respawner = default!;

    public HttpClient HttpClient { get; private set; } = default!;
    public string ConnectionString { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        // 1. Start SQL Server container
        await _sqlContainer.StartAsync();
        ConnectionString = _sqlContainer.GetConnectionString();

        // 2. Apply EF Core migrations against the container
        var optionsBuilder = new DbContextOptionsBuilder<PyroDbContext>()
            .UseSqlServer(ConnectionString);
        await using var context = new PyroDbContext(optionsBuilder.Options);
        await context.Database.MigrateAsync();

        // 3. Create Respawn checkpoint — only reset FHIR resource and index tables.
        //    SearchParameterStore, ServiceBaseUrl, and ServiceSetting must not be cleared:
        //    SearchParameterStore and ServiceBaseUrl are seeded by startup services;
        //    ServiceSetting is seeded by EF migrations (default FhirValidation row) and
        //    SingleAsync in ServiceConfigurationGetCurrentByType throws if it is absent.
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            TablesToInclude =
            [
                new Respawn.Graph.Table("ResourceStore"),
                new Respawn.Graph.Table("IndexString"),
                new Respawn.Graph.Table("IndexReference"),
                new Respawn.Graph.Table("IndexDateTime"),
                new Respawn.Graph.Table("IndexQuantity"),
                new Respawn.Graph.Table("IndexToken"),
                new Respawn.Graph.Table("IndexUri"),
            ]
        });

        // 4. Start WebApplicationFactory (this triggers the kept startup services,
        //    including FhirServiceBaseUrlManagementOnStartupService which seeds ServiceBaseUrl)
        _factory = new PyroWebApplicationFactory(ConnectionString);

        // 5. Create the shared HttpClient — base address is the test server
        HttpClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    public async Task DisposeAsync()
    {
        HttpClient.Dispose();
        await _factory.DisposeAsync();
        await _sqlContainer.DisposeAsync();
    }
}
