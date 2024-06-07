using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlUpdateSimultaneous
{
  Task Update(ServiceBaseUrl serviceBaseUrlOne, ServiceBaseUrl serviceBaseUrlTwo);
}
