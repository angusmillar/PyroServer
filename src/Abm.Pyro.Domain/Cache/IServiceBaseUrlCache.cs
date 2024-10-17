
namespace Abm.Pyro.Domain.Cache;

public interface IServiceBaseUrlCache
{
  Task<Model.ServiceBaseUrl?> GetPrimaryAsync();
  Task<Model.ServiceBaseUrl> GetRequiredPrimaryAsync();
  Task<Model.ServiceBaseUrl?> GetByUrlAsync(string url);
  Task Remove(string url);
  Task RemovePrimary();
}
