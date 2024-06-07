using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlAddByUri(PyroDbContext context) : IServiceBaseUrlAddByUri
{
  public async Task<ServiceBaseUrl> Add(ServiceBaseUrl serviceBaseUrl)
  {
    context.Add(serviceBaseUrl);
    await context.SaveChangesAsync();
    return serviceBaseUrl;
  }
}
