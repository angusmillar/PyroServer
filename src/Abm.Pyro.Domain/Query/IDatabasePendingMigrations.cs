namespace Abm.Pyro.Domain.Query;

public interface IDatabasePendingMigrations
{
    Task<string[]> Get();
}