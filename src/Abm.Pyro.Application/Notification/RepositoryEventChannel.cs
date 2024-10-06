using System.Globalization;
using System.Threading.Channels;
using Abm.Pyro.Repository.DependencyFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.Notification;

public class RepositoryEventChannel(
    ILogger<RepositoryEventChannel> logger,
    IServiceScopeFactory serviceScopeFactory) : IRepositoryEventChannel
{
    private readonly Channel<ICollection<RepositoryEvent>> _channel = Channel.CreateUnbounded<ICollection<RepositoryEvent>>();


    public async Task AddAsync(ICollection<RepositoryEvent> repositoryEventList)
    {
        await _channel.Writer.WriteAsync(repositoryEventList);
    }

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await foreach (ICollection<RepositoryEvent> repositoryEventList in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            await Task.Delay(10, cancellationToken: cancellationToken);
            using var scope = serviceScopeFactory.CreateScope();
            try
            {
                var pyroDbContextFactory = scope.ServiceProvider.GetRequiredService<IPyroDbContextFactory>();
                
                
                
                logger.LogInformation("RepositoryEventQueue Queue Count: {RepositoryEventCount}",
                    repositoryEventList.Count);
            
                foreach (RepositoryEvent repositoryEvent in repositoryEventList)
                {
                    logger.LogInformation("{@RepositoryEvent}", repositoryEvent);

                    var pyroDbContext = pyroDbContextFactory.Get(repositoryEvent.Tenant);
                    var resourceStore = await pyroDbContext.ResourceStore.FindAsync(repositoryEvent.ResourceStoreId, cancellationToken);


                }
                
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Bad");
            }
        }
    }
}