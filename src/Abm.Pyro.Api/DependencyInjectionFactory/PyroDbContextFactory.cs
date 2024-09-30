using Abm.Pyro.Application.Tenant;
using Abm.Pyro.Repository;
using Abm.Pyro.Repository.DependencyFactory;
using Microsoft.EntityFrameworkCore;

namespace Abm.Pyro.Api.DependencyInjectionFactory;

public class PyroDbContextFactory(IServiceProvider serviceProvider) : IPyroDbContextFactory
{
    public PyroDbContext Get()
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var tenantService = serviceProvider.GetRequiredService<ITenantService>();
        
        string? connectionString = configuration.GetConnectionString(tenantService.GetScopedTenant().SqlConnectionStringCode);
        
        DbContextOptionsBuilder dbContextOptionsBuilder = new DbContextOptionsBuilder<PyroDbContext>();
        dbContextOptionsBuilder.UseSqlServer(connectionString)
            .EnableSensitiveDataLogging(true);

        return new PyroDbContext((DbContextOptions<PyroDbContext>)dbContextOptionsBuilder.Options);
         
    }
}