using Abm.Pyro.Domain.Query;
using Microsoft.EntityFrameworkCore;

namespace Abm.Pyro.Repository.Query;

public class DatabasePendingMigrations(PyroDbContext context) : IDatabasePendingMigrations
{
    public async Task<string[]> Get()
    {
        IEnumerable<string> pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        return pendingMigrations as string[] ?? pendingMigrations.ToArray();
    }
}