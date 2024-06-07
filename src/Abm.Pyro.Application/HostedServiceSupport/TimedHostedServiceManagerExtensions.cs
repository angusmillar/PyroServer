using Microsoft.Extensions.DependencyInjection;

namespace Abm.Pyro.Application.HostedServiceSupport;

public static class TimedHostedServiceManagerExtensions
{
    public static void AddTimedHostedService<T>(this IServiceCollection services,
        Action<TimedHostedServiceManagerOptions<T>> configurator) where T : class, ITimedHostedService
    {
        var options = new TimedHostedServiceManagerOptions<T>();
        configurator(options);
        services.AddSingleton(x => options);
        services.AddScoped<T>();
        services.AddHostedService<TimedHostedServiceManager<T>>();
    }
}