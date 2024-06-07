using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
namespace Abm.Pyro.Repository.Query;

public class ServiceBaseUrlUpdateSimultaneous(PyroDbContext context) : IServiceBaseUrlUpdateSimultaneous
{
  public async Task Update(ServiceBaseUrl serviceBaseUrlOne, ServiceBaseUrl serviceBaseUrlTwo)
  {
    context.Update(serviceBaseUrlOne);
    context.Update(serviceBaseUrlTwo);
    await context.SaveChangesAsync(); //I suspect this save should not be here?
  }
}
