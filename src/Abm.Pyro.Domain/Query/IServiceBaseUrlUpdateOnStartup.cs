using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlUpdateOnStartup
{
  Task<ServiceBaseUrl> Update(ServiceBaseUrl serviceBaseUrl);
}
