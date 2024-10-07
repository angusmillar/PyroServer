using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Application.Notification;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.Manager;
// ReSharper disable once ClassNeverInstantiated.Global
public class NotificationManager(
    ILogger<NotificationManager> logger,
    IRepositoryEventChannel repositoryEventChannel) : ITimedHostedService
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        try
        {
            await repositoryEventChannel.ProcessAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Uncaught exception in the IRepositoryEventChannel class");
            throw;
        }
    }
}