using System.Threading.Channels;
using Abm.Pyro.Application.FhirSubscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.Notification;

public class RepositoryEventChannel(
    ILogger<RepositoryEventChannel> logger,
    IServiceScopeFactory serviceScopeFactory,
    IFhirNotificationService fhirNotificationService) : IRepositoryEventChannel
{
    private readonly Channel<ICollection<RepositoryEvent>> _channel = Channel.CreateBounded<ICollection<RepositoryEvent>>(
        new BoundedChannelOptions(capacity: 5000)
        {
            FullMode = BoundedChannelFullMode.Wait 
        });

    public async Task AddAsync(ICollection<RepositoryEvent> repositoryEventList)
    {
        await _channel.Writer.WriteAsync(repositoryEventList);
    } 

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await foreach (ICollection<RepositoryEvent> repositoryEventList in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            //await Task.Delay(10, cancellationToken: cancellationToken);
            using var scope = serviceScopeFactory.CreateScope();
            try
            {
                await fhirNotificationService.ProcessEventList(repositoryEventList);
                
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Uncaught exception in the {name} class", nameof(RepositoryEventChannel));
            }
        }
    }
}