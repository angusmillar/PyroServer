using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlGetPrimaryOnStartup
{
  Task<ServiceBaseUrl?> Get();
}
