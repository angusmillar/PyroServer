using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlUpdate
{
  Task<ServiceBaseUrl> Update(ServiceBaseUrl serviceBaseUrl);
}
