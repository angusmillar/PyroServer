using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Repository.DependencyFactory;

namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlGetPrimaryOnStartup(
    IPyroDbContextFactory pyroDbContextFactory) : IServiceBaseUrlGetPrimaryOnStartup
{
  public async Task<ServiceBaseUrl?> Get()
  {
      using (PyroDbContext context = pyroDbContextFactory.Get())
      {
          return await context.Set<ServiceBaseUrl>().SingleOrDefaultAsync(x => x.IsPrimary == true);
      }
  }
}
