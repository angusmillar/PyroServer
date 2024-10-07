using System.Threading.Channels;
using Abm.Pyro.Application.FhirSubscriptions;
using Abm.Pyro.Application.Tenant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.Notification;

public class RepositoryEventChannel(
    ILogger<RepositoryEventChannel> logger,
    IHostEnvironment hostEnvironment,
    IServiceScopeFactory serviceScopeFactory) : IRepositoryEventChannel
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
            if (repositoryEventList.Count == 0)
            {
                continue;
            }

            ThrowIfInvalidTenants(repositoryEventList);
            
            //Helps with log messages order, but needs to go for production deployments
            if (hostEnvironment.IsDevelopment())
            {
                await Task.Delay(5, cancellationToken: cancellationToken);    
            }
            
            using var scope = serviceScopeFactory.CreateScope();
            try
            {
                ITenantService tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                tenantService.SetScopedTenant(repositoryEventList.First().Tenant);
                
                IFhirNotificationService fhirNotificationService = scope.ServiceProvider.GetRequiredService<IFhirNotificationService>();
                await fhirNotificationService.ProcessEventList(repositoryEventList);
                
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Uncaught exception in the {name} class", nameof(RepositoryEventChannel));
            }
        }
    }
    private static void ThrowIfInvalidTenants(ICollection<RepositoryEvent> repositoryEventList)
    {
        if (!repositoryEventList.All(x => x.Tenant.Equals(repositoryEventList.First().Tenant)))
        {
            throw new ApplicationException($"All Repository Events in a collection must have the same {nameof(Tenant)}");
        }
    }
    
}