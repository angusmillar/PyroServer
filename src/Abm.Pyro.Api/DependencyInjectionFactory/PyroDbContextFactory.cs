using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Repository;
using Abm.Pyro.Repository.DependencyFactory;
using Microsoft.EntityFrameworkCore;

namespace Abm.Pyro.Api.DependencyInjectionFactory;

public class PyroDbContextFactory(IServiceProvider serviceProvider, IWebHostEnvironment env) : IPyroDbContextFactory
{
    public PyroDbContext Get(Tenant tenant)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        
        string? connectionString = configuration.GetConnectionString(tenant.SqlConnectionStringCode);
        
        DbContextOptionsBuilder dbContextOptionsBuilder = new DbContextOptionsBuilder<PyroDbContext>();
        dbContextOptionsBuilder.UseSqlServer(connectionString)
            .EnableSensitiveDataLogging(env.IsDevelopment());

        return new PyroDbContext((DbContextOptions<PyroDbContext>)dbContextOptionsBuilder.Options);
         
    }
}
