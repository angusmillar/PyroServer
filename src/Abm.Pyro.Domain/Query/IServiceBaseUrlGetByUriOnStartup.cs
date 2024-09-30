using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlGetByUriOnStartup
{
  Task<ServiceBaseUrl?> Get(string url);
}
