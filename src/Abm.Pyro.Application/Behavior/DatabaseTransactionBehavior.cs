using Abm.Pyro.Application.Notification;
using Abm.Pyro.Domain.FhirRequest;
using Abm.Pyro.Domain.FhirResponse;
using Abm.Pyro.Domain.Notification;
using Abm.Pyro.Domain.Dispatcher;
using Microsoft.Extensions.Logging;
using Abm.Pyro.Domain.Query;

namespace Abm.Pyro.Application.Behavior;

public class DatabaseTransactionBehavior<TRequest, TResponse>(
    ILogger<DatabaseTransactionBehavior<TRequest, TResponse>> logger,
    IDatabaseTransactionFactory databaseTransactionFactory,
    IRepositoryEventChannel repositoryEventChannel)
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
            
            TransactionResponse? transactionResponse = TrySetTransactionResponse(response);
            if (CanCommitTransaction(transactionResponse))
            {
                await databaseTransaction.Commit();
                await AttemptToPublishRepositoryEvents(request, transactionResponse);
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

    private async Task AttemptToPublishRepositoryEvents(
        TRequest request,
        TransactionResponse? transactionResponse)
    {
        string? requestId = GetRequestId(request);
        if (requestId is null)
        {
            return;
        }

        await PublishRepositoryEvents(requestId, transactionResponse);
    }

    private static string? GetRequestId(TRequest request)
    {
        if (request is FhirRequestBase fhirRequestBase && !string.IsNullOrWhiteSpace(fhirRequestBase.RequestId))
        {
            return fhirRequestBase.RequestId;
        }

        return null;
    }

    private async Task PublishRepositoryEvents(string requestId, TransactionResponse? transactionResponse)
    {
        if (transactionResponse is null)
        {
            return;
        }
        
        await repositoryEventChannel.AddAsync(
            new RepositoryEventSet(
                RequestId: requestId, 
                RepositoryEventList: transactionResponse.RepositoryEventCollector.RepositoryEventList));
    }
    
    private bool CanCommitTransaction(TransactionResponse? transactionResponse)
    {
        if (transactionResponse is null)
        {
            return false;
        }

        return transactionResponse.CanCommitTransaction;
    }

    private TransactionResponse? TrySetTransactionResponse(TResponse response)
    {
        if (response is TransactionResponse transactionResponse)
        {
            return transactionResponse;
        }

        return null;
    }
}