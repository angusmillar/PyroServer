using Microsoft.EntityFrameworkCore.Storage;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Repository.Query;

public class DatabaseTransaction(PyroDbContext context) : IDatabaseTransaction, IDisposable
{
    private IDbContextTransaction? _dbContextTransaction;

    public async Task BeginTransaction()
    {
        _dbContextTransaction = await context.Database.BeginTransactionAsync();
    }

    public async Task Commit()
    {
        if (_dbContextTransaction is null)
        {
            throw new NullReferenceException(nameof(_dbContextTransaction));
        }
        await _dbContextTransaction.CommitAsync();
    }

    public async Task RollBack()
    {
        if (_dbContextTransaction is null)
        {
            throw new NullReferenceException(nameof(_dbContextTransaction));
        }
        await _dbContextTransaction.RollbackAsync();
    }

    public void Dispose()
    {
        context.Dispose();
        _dbContextTransaction?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await context.DisposeAsync();
        if (_dbContextTransaction != null) await _dbContextTransaction.DisposeAsync();
    }
}