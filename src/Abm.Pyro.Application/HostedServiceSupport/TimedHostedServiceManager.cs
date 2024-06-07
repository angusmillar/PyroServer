using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.HostedServiceSupport;

/// <summary>
/// This class managers running a provided scoped service that implements the ITimedHostedService interface 
/// This class ensures the ITimedHostedService triggers ever configurable TimeSpan (e.g TimerThenFiresEvery).
/// Each time the timer triggers it only starts a new Task if the previous one has completed.
/// </summary>
/// <typeparam name="T"></typeparam>
public class TimedHostedServiceManager<T>(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TimedHostedServiceManager<T>> logger,
    TimedHostedServiceManagerOptions<T> options)
    : IHostedService, IDisposable
    where T : ITimedHostedService
{
    private readonly string TaskName = typeof(T).Name;

    private Task? ExecutingTask = null;
    private CancellationTokenSource? StoppingCancellationTokenSource;

    /// <summary>
    /// Call by the HostedService framework to Start a IHostedService instance 
    /// This creates a Task which runs a PeriodicTimer for the IHostedService instance
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.TriggersEvery == TimeSpan.Zero)
        {
            LogThatTaskWillNotBeRun();
        }
        else
        {
            //We don't want to await this call as it is to run in the background
            //furthermore awaiting here will prevent the entire application startup from completing
            ExecutingTask = StartServicePeriodicTimerTask(cancellationToken);
        }

        return Task.CompletedTask;
    }

    private void LogThatTaskWillNotBeRun()
    {
        logger.LogInformation("{Interface} disabled {TaskName} as trigger interval set to zero",
            nameof(ITimedHostedService),
            TaskName
        );
    }

    /// <summary>
    /// Each time the timer triggers it creates a new scope from the IServiceScopeFactory.
    /// It then gets the 'T' IHostedService instance from the scopes ServiceProvider.
    /// It calls  and awaits the DoWork method of the IHostedService providing it a cancellationToken.
    /// Only when the awaited DoWork Task in completed can the PeriodicTimer trigger again. 
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task StartServicePeriodicTimerTask(CancellationToken cancellationToken)
    {
        logger.LogInformation("{Interface} commenced {TaskName}, triggering every: {TimerThenFiresEvery}",
            nameof(ITimedHostedService),
            TaskName,
            FormatTimeSpan(options.TriggersEvery)
        );

        StoppingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var periodicTimer = new PeriodicTimer(options.TriggersEvery);
        try
        {
            while (await periodicTimer.WaitForNextTickAsync(StoppingCancellationTokenSource.Token) && !cancellationToken.IsCancellationRequested)
            {
                await ExecuteTickEvent(StoppingCancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("{TaskName} PeriodicTimer canceled", TaskName);
        }
    }

    private async Task ExecuteTickEvent(CancellationToken stoppingCancellationToken)
    {
        logger.LogDebug("{TaskName} Started", TaskName);
        using var scope = serviceScopeFactory.CreateScope();
        try
        {
            var serviceToRun = scope.ServiceProvider.GetRequiredService<T>();
            await serviceToRun.DoWork(stoppingCancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "{TaskName} has thrown an unhandled exception", TaskName);
        }

        logger.LogDebug("{TaskName} Completed ", TaskName);
    }

    /// <summary>
    /// When the framework call this method to stop
    /// The CancellationToken which has been passed to all the running IHostedService instances is Canceled.
    /// We then WaitALL of the IHostedService's PeriodicTimers Tasks to end before completing the Stop.
    /// This should give the IHostedServices time to complete gracefully, before the system forcefully goes down.    
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogDebug("{TaskName} Terminating gracefully", TaskName);
        if (StoppingCancellationTokenSource is not null)
        {
            StoppingCancellationTokenSource.Cancel();
        }

        if (ExecutingTask is not null)
        {
            await ExecutingTask.WaitAsync(cancellationToken);
        }

        logger.LogInformation("{TaskName} Terminated", TaskName);
    }


    /// <summary>
    /// Formats TimeSpan into a human friendly string format for logging. 
    /// </summary>
    /// <param name="timeSpan"></param>
    /// <returns></returns>
    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (IsMilliseconds())
        {
            return $"{timeSpan.Milliseconds} millisecond";
        }

        if (IsSeconds())
        {
            return $"{timeSpan.Seconds} sec";
        }

        if (IsMinutes())
        {
            if (HasZeroSeconds())
            {
                return $"{timeSpan.Minutes} min";
            }

            return $"{timeSpan.Minutes} min, {timeSpan.Seconds} sec";
        }

        if (IsHours())
        {
            if (HasZeroMinutes() && HasZeroSeconds())
            {
                return $"{timeSpan.Hours} hours";
            }

            if (HasZeroSeconds())
            {
                return $"{timeSpan.Hours} hours, {timeSpan.Minutes} min";
            }

            return $"{timeSpan.Hours} hours, {timeSpan.Minutes} min, {timeSpan.Seconds} sec";
        }

        return $"{timeSpan.Days} days, {timeSpan.Hours} hours, {timeSpan.Minutes} min, {timeSpan.Seconds} sec";

        bool IsMilliseconds()
        {
            return timeSpan < TimeSpan.FromSeconds(1);
        }

        bool IsSeconds()
        {
            return timeSpan < TimeSpan.FromMinutes(1);
        }

        bool IsMinutes()
        {
            return timeSpan < TimeSpan.FromHours(1);
        }

        bool IsHours()
        {
            return timeSpan < TimeSpan.FromDays(1);
        }

        bool HasZeroSeconds()
        {
            return timeSpan.Seconds == 0;
        }

        bool HasZeroMinutes()
        {
            return timeSpan.Minutes == 0;
        }
    }

    public void Dispose()
    {
        StoppingCancellationTokenSource?.Cancel();
        GC.SuppressFinalize(this);
    }
}