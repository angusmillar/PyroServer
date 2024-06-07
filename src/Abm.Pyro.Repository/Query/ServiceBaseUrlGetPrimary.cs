using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlGetPrimary(PyroDbContext context) : IServiceBaseUrlGetPrimary
{
  public Task<ServiceBaseUrl?> Get()
  {
    return context.Set<ServiceBaseUrl>().SingleOrDefaultAsync(x => x.IsPrimary == true);
  }
}
