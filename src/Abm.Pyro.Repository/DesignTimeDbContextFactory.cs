using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Abm.Pyro.Domain.Configuration;

namespace Abm.Pyro.Repository;

/// <summary>
/// Friends, PLEASE NOTE, the AddJsonFile("appsettings.json") method is not aware of your hosting environment! 
/// Use .AddJsonFile($"appsettings.{_hostingEnvironment.EnvironmentName}.json") instead.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PyroDbContext>

{
  // public DesignTimeDbContextFactory()
  // {
  //   //Use below to debug this class while running Migration commands.
  //   Debugger.Launch();
  // }

  public PyroDbContext CreateDbContext(string[] args)
  {
    // IDesignTimeDbContextFactory is used usually when you execute EF Core commands like Add-Migration, Update-Database, and so on
    // So it is usually your local development machine environment

    // Prepare configuration builder
    var configuration = new ConfigurationBuilder()
                        .SetBasePath(Path.Combine(Directory.GetCurrentDirectory()))
                        .AddJsonFile("appsettings.json", optional: false)
                        .Build();
    var databaseSettingsSection = configuration.GetSection(DatabaseSettings.SectionName); 
    DatabaseSettings? databaseSettings =  databaseSettingsSection.Get<DatabaseSettings>();
    if (databaseSettings is null)
    {
      throw new NoNullAllowedException(nameof(databaseSettings));
    }
    
    // Create DB context with connection from your AppSettings 
    var optionsBuilder = new DbContextOptionsBuilder<PyroDbContext>()
      .UseSqlServer(databaseSettings.ConnectionString);

    return new PyroDbContext(optionsBuilder.Options);
  }
}
