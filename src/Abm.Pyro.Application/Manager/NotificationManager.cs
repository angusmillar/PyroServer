using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Application.Notification;

namespace Abm.Pyro.Application.Manager;
// ReSharper disable once ClassNeverInstantiated.Global
public class NotificationManager(
    IRepositoryEventChannel repositoryEventChannel) : ITimedHostedService
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        await repositoryEventChannel.ProcessAsync(cancellationToken);
    }
}