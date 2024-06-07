namespace Abm.Pyro.Domain.Query;

public interface IDatabaseTransaction : IAsyncDisposable
{
    Task BeginTransaction();
    Task Commit();
    Task RollBack();
}