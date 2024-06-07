namespace Abm.Pyro.Domain.Query;

public interface IDatabaseTransactionFactory
{
    IDatabaseTransaction GetTransaction();
}