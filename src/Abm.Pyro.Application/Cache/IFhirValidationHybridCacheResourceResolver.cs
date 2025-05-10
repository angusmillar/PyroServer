using Task = System.Threading.Tasks.Task;

namespace Abm.Pyro.Application.Cache;

public interface IFhirValidationHybridCacheResourceResolver
{
    Task RefreshCache();
}