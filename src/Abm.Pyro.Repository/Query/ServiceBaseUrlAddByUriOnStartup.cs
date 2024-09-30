using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Repository.DependencyFactory;

namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlAddByUriOnStartup(IPyroDbContextFactory pyroDbContextFactory) : IServiceBaseUrlAddByUriOnStartup
{
  public async Task<ServiceBaseUrl> Add(ServiceBaseUrl serviceBaseUrl)
  {
    await using (PyroDbContext context = pyroDbContextFactory.Get())
    {
      context.Add(serviceBaseUrl);
      await context.SaveChangesAsync();
      return serviceBaseUrl;
    }
  }
}
