using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlGetByUri(PyroDbContext context) : IServiceBaseUrlGetByUri
{
  public async Task<ServiceBaseUrl?> Get(string url)
  {
    return await context.Set<ServiceBaseUrl>().SingleOrDefaultAsync(x => x.Url == url);
  }
}
