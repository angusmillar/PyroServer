using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Repository.DependencyFactory;

namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlGetByUriOnStartup(IPyroDbContextFactory pyroDbContextFactory) : IServiceBaseUrlGetByUriOnStartup
{
  public Task<ServiceBaseUrl?> Get(string url)
  {
    using (PyroDbContext context = pyroDbContextFactory.Get())
    {
      return context.Set<ServiceBaseUrl>().SingleOrDefaultAsync(x => x.Url == url);
    }
  }
}
