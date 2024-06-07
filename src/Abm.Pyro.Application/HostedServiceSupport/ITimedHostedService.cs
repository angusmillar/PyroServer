namespace Abm.Pyro.Application.HostedServiceSupport;

/// <summary>
/// This interface can be implemented and run as a background hosted service using the service extention services.AddTimedHostedService&amp;lt;T&amp;gt;()
/// which allows you to configure how often the service is run and how long before it first runs.
/// If the background hosted service takes longer to run than its given cycle time 'TimerThenFiresEvery'. 
/// Then that execution is skipped until the next timer cycle fires, where it will check for completion again.
///   /// Below is an example service registration where 'MyTimedHostedService' implements this 'ITimedHostedService' interface:
/// 
/// services.AddScoped&lt;MyTimedHostedService&gt;();
/// services.AddTimedHostedService&lt;MyTimedHostedService&gt;(options =&gt;
///    {
///      options.TimerFirstFiresAt = TimeSpan.FromSeconds(1);
///      options.TimerThenFiresEvery = TimeSpan.FromSeconds(2);
///    });
/// You can also repeat the above registrations to run many different services on different timers.
/// </summary>
public interface ITimedHostedService
{
    public Task DoWork(CancellationToken cancellationToken);
}