namespace Abm.Pyro.Application.HostedServiceSupport;

public class TimedHostedServiceManagerOptions<T> where T : ITimedHostedService
{
    public TimeSpan TriggersEvery { get; set; } = TimeSpan.FromSeconds(30);
}