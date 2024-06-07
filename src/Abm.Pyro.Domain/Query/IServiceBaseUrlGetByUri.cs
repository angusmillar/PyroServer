using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlGetByUri
{
  Task<ServiceBaseUrl?> Get(string url);
}
