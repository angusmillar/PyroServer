using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlAddByUri
{
  Task<ServiceBaseUrl>  Add(ServiceBaseUrl serviceBaseUrl);
}
