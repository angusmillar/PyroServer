using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Repository.Query;

public class DatabaseTransaction(PyroDbContext context) : IDatabaseTransaction, IDisposable, IAsyncDisposable
{
    private IDbContextTransaction? DbContextTransaction;

    public async Task BeginTransaction()
    {
        DbContextTransaction = await context.Database.BeginTransactionAsync();
    }

    public async Task Commit()
    {
        if (DbContextTransaction is null)
        {
            throw new NullReferenceException(nameof(DbContextTransaction));
        }
        await DbContextTransaction.CommitAsync();
    }

    public async Task RollBack()
    {
        if (DbContextTransaction is null)
        {
            throw new NullReferenceException(nameof(DbContextTransaction));
        }
        await DbContextTransaction.RollbackAsync();
    }

    public void Dispose()
    {
        context.Dispose();
        DbContextTransaction?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
        if (DbContextTransaction != null) await DbContextTransaction.DisposeAsync();
    }
}