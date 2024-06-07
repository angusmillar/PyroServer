namespace Abm.Pyro.Application.HostedServiceSupport;

public interface IAppStartupService
{
    public Task DoWork(CancellationToken cancellationToken);
}