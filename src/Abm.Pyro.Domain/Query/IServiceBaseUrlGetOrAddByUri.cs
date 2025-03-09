using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlGetOrAddByUri
{
    Task<ServiceBaseUrl> Get(string url);
}