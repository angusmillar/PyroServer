using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlGetPrimary
{
  Task<ServiceBaseUrl?> Get();
}
