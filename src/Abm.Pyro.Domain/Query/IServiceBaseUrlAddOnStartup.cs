using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlAddByUriOnStartup
{
  Task<ServiceBaseUrl>  Add(ServiceBaseUrl serviceBaseUrl);
}
