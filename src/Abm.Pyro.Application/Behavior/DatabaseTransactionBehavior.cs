using Abm.Pyro.Application.FhirResponse;
using MediatR;
using Microsoft.Extensions.Logging;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Application.Behavior;

public class DatabaseTransactionBehavior<TRequest, TResponse>(
  ILogger<DatabaseTransactionBehavior<TRequest, TResponse>> logger,
  IDatabaseTransactionFactory databaseTransactionFactory)
  : IPipelineBehavior<TRequest, TResponse>
  where TRequest : notnull
{
  public async Task<TResponse> Handle(
    TRequest request, 
    RequestHandlerDelegate<TResponse> next, 
    CancellationToken cancellationToken)
  {
    await using IDatabaseTransaction databaseTransaction = databaseTransactionFactory.GetTransaction();
    await databaseTransaction.BeginTransaction();
    try
    {
      var response = await next();
      if (CanCommitTransaction(response))
      {
        await databaseTransaction.Commit();
        return response;
      }
      
      await databaseTransaction.RollBack();
      logger.LogInformation("Database transaction was requested to be rolled back ");
      return response;
    }
    catch (Exception e)
    {
      await databaseTransaction.RollBack();
      logger.LogError(e, "Database transaction has been rolled back due to an unhandled Exception ");
      throw;
    }
  }

  private static bool CanCommitTransaction(TResponse response)
  {
    if (response is TransactionResponse transactionResponse)
    {
      return transactionResponse.CanCommitTransaction;
    }
    return true;
  }
}
