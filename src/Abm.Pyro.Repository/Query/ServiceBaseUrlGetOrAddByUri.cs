using Microsoft.EntityFrameworkCore;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlGetOrAddByUri(PyroDbContext context) : IServiceBaseUrlGetOrAddByUri
{
  public async Task<ServiceBaseUrl> Get(string url)
  {
    ServiceBaseUrl? serviceBaseUrl = await context.Set<ServiceBaseUrl>().SingleOrDefaultAsync(x => x.Url == url);
    if (serviceBaseUrl is null)
    {
      serviceBaseUrl = new ServiceBaseUrl(
        serviceBaseUrlId: null,
        url: url,
        isPrimary: false);
      context.Set<ServiceBaseUrl>().Add(serviceBaseUrl);
      
      return serviceBaseUrl;
    }
    
    return serviceBaseUrl;
  }
}
