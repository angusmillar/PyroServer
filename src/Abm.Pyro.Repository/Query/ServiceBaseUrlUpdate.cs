using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlUpdate(PyroDbContext context) : IServiceBaseUrlUpdate
{
  public async Task<ServiceBaseUrl> Update(ServiceBaseUrl serviceBaseUrl)
  {
    context.Update(serviceBaseUrl);
    await context.SaveChangesAsync();
    return serviceBaseUrl;
  }
}
