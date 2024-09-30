using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Repository.DependencyFactory;

namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlUpdateOnStartup(IPyroDbContextFactory pyroDbContextFactory) : IServiceBaseUrlUpdateOnStartup
{
  public async Task<ServiceBaseUrl> Update(ServiceBaseUrl serviceBaseUrl)
  {
    await using (PyroDbContext context = pyroDbContextFactory.Get())
    {
      context.Update(serviceBaseUrl);
      await context.SaveChangesAsync();
      return serviceBaseUrl;
    }
  }
}
