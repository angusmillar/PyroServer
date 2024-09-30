using Abm.Pyro.Domain.Query;
using Abm.Pyro.Repository.DependencyFactory;
using Microsoft.EntityFrameworkCore;

namespace Abm.Pyro.Repository.Query;

public class DatabasePendingMigrations(
    IPyroDbContextFactory pyroDbContextFactory) : IDatabasePendingMigrations
{
    public async Task<string[]> Get()
    {
        using (PyroDbContext context = pyroDbContextFactory.Get())
        {
            IEnumerable<string> pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        
            return pendingMigrations as string[] ?? pendingMigrations.ToArray();     
        }

    }
}