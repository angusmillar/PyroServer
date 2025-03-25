using System.Threading.Channels;
using Abm.Pyro.Application.FhirSubscriptions;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Enums;
using Abm.Pyro.Domain.Notification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.Notification;

public class RepositoryEventChannel(
    ILogger<RepositoryEventChannel> logger,
    IHostEnvironment hostEnvironment,
    IServiceScopeFactory serviceScopeFactory) : IRepositoryEventChannel
{
    private readonly Channel<RepositoryEventSet> _channel = Channel.CreateBounded<RepositoryEventSet>(
        new BoundedChannelOptions(capacity: 5000)
        {
            FullMode = BoundedChannelFullMode.Wait 
        });

    public async Task AddAsync(RepositoryEventSet repositoryEventList)
    {
        await _channel.Writer.WriteAsync(repositoryEventList);
    } 

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await foreach (RepositoryEventSet repositoryEventSet in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            if (repositoryEventSet.RepositoryEventList.Count == 0)
            {
                continue;
            }

            ThrowIfInvalidTenants(repositoryEventSet);
            
            //Helps with log messages order, but needs to go for production deployments
            if (hostEnvironment.IsDevelopment())
            {
                await Task.Delay(5, cancellationToken: cancellationToken);    
            }
            
            foreach (var repositoryEvent in repositoryEventSet.RepositoryEventList)
            {
                logger.LogDebug("Repository Event Raised for Tenant: {Tenant}, EventType: {EventType}, " +
                                "Resource: {ResourceType}/{ResourceId} ", 
                    repositoryEvent.Tenant.Code, 
                    repositoryEvent.RepositoryEventType.GetCode(), 
                    repositoryEvent.ResourceType.GetCode(), 
                    repositoryEvent.ResourceId);
            }
            
            using var scope = serviceScopeFactory.CreateScope();
            try
            {
                ITenantService tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                tenantService.SetScopedTenant(repositoryEventSet.RepositoryEventList.First().Tenant);
                
                IFhirNotificationService fhirNotificationService = scope.ServiceProvider.GetRequiredService<IFhirNotificationService>();
                await fhirNotificationService.ProcessEventList(repositoryEventSet, cancellationToken);
                
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Uncaught exception in the {ClassName} class", nameof(RepositoryEventChannel));
            }
        }
    }
    private static void ThrowIfInvalidTenants(RepositoryEventSet repositoryEventSet)
    {
        if (!repositoryEventSet.RepositoryEventList.All(x => x.Tenant.Equals(repositoryEventSet.RepositoryEventList.First().Tenant)))
        {
            throw new ApplicationException($"All Repository Events in a RepositoryEventSet must have the same {nameof(Tenant)}");
        }
    }
    
}