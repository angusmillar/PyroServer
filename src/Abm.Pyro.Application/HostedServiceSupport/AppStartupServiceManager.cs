using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.HostedServiceSupport;

public class AppStartupServiceManager<T>(
    ILogger<AppStartupServiceManager<T>> logger,
    IServiceScopeFactory serviceScopeFactory) : IHostedService, IDisposable
    where T : IAppStartupService
{
    private CancellationTokenSource? StoppingCancellationTokenSource;

    private readonly string TaskName = typeof(T).Name;
    private Task? ExecutingTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        StoppingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        logger.LogInformation("{Interface} commenced {TaskName}",
            nameof(IAppStartupService),
            TaskName
        );
        ExecutingTask = StartServicePeriodicTimerTask();
        await ExecutingTask.WaitAsync(StoppingCancellationTokenSource.Token);
        ExecutingTask = null;
        
        logger.LogInformation("{Interface} completed {TaskName}",
            nameof(IAppStartupService),
            TaskName
        );
    }

    private async Task StartServicePeriodicTimerTask()
    {
        if (StoppingCancellationTokenSource is null)
        {
            throw new NullReferenceException(nameof(StoppingCancellationTokenSource));
        }
        using var scope = serviceScopeFactory.CreateScope();
        try
        {
            var serviceToRun = scope.ServiceProvider.GetRequiredService<T>();
            await serviceToRun.DoWork(StoppingCancellationTokenSource.Token);
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "{Interface} of type {TaskName} has thrown an unhandled exception preventing the application from starting",nameof(IAppStartupService), TaskName);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        StoppingCancellationTokenSource?.Cancel();
        if (ExecutingTask is not null)
        {
            logger.LogError("{Interface} unexpectedly required to terminate {TaskName}", nameof(IAppStartupService), TaskName);
            await ExecutingTask.WaitAsync(cancellationToken);
            logger.LogError("{Interface} terminated successfully {TaskName}", nameof(IAppStartupService), TaskName);
        }
    }

    public void Dispose()
    {
        StoppingCancellationTokenSource?.Cancel();
        GC.SuppressFinalize(this);
    }
}