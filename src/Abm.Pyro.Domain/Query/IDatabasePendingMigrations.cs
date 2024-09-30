using Abm.Pyro.Domain.Configuration;

namespace Abm.Pyro.Domain.Query;

public interface IDatabasePendingMigrations
{
    Task<string[]> Get(Tenant tenant);
}