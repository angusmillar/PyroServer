using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Repository.Query;

public class DatabaseTransactionFactory(IDatabaseTransaction databaseTransaction) : IDatabaseTransactionFactory
{
    public IDatabaseTransaction GetTransaction()
    {
        return databaseTransaction;
    }
}