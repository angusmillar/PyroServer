using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Cache;

public interface IServiceBaseUrlCache
{
  Task<ServiceBaseUrl?> GetPrimaryAsync();
  Task<ServiceBaseUrl> GetRequiredPrimaryAsync();
  Task<ServiceBaseUrl?> GetByUrlAsync(string url);
  Task Remove(string url);
  Task RemovePrimary();
}
