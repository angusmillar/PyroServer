using System.Globalization;
using Abm.Pyro.Application.FhirResponse;
using Abm.Pyro.Application.Notification;
using MediatR;
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
    private TransactionResponse? _transactionResponse;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _transactionResponse = null;
        await using IDatabaseTransaction databaseTransaction = databaseTransactionFactory.GetTransaction();
        await databaseTransaction.BeginTransaction();
        try
        {
            var response = await next();

            TrySetTransactionResponse(response);
            if (CanCommitTransaction())
            {
                await databaseTransaction.Commit();
                await PublishRepositoryEvents();
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

    private async Task PublishRepositoryEvents()
    {
        if (_transactionResponse?.RepositoryEventCollector is null)
        {
            return;
        }

        await repositoryEventChannel.AddAsync(_transactionResponse.RepositoryEventCollector.RepositoryEventList.ToList());
        //
        // logger.LogInformation("RepositoryEventQueue Queue Count: {RepositoryEventCount}", _transactionResponse.RepositoryEventCollector.RepositoryEventList.Count);    
        // foreach (var repositoryEvent in _transactionResponse.RepositoryEventCollector.RepositoryEventList)
        // {
        //     logger.LogInformation("RepositoryEventType: {RepositoryEventType}, ResourceStoreId: {ResourceStoreId}, EventTimestampUtc: {EventTimestampUtc}",
        //         repositoryEvent.RepositoryEventType,
        //         repositoryEvent.ResourceStoreId,
        //         repositoryEvent.EventTimestampUtc.ToString(CultureInfo.InvariantCulture));    
        // }
        
    }

    private bool CanCommitTransaction()
    {
        if (_transactionResponse is null)
        {
            return false;
        }

        return _transactionResponse.CanCommitTransaction;
    }

    private void TrySetTransactionResponse(TResponse response)
    {
        if (_transactionResponse is null && response is TransactionResponse transactionResponse)
        {
            _transactionResponse = transactionResponse;
        }
    }
}