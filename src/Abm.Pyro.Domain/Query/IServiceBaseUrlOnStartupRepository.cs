using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;

namespace Abm.Pyro.Domain.Query;

public interface IServiceBaseUrlOnStartupRepository
{
    void StartUnitOfWork(Tenant tenant);

    bool IsUnitOfWorkStarted();

    Task<int> SaveChangesAsync();

    Task DisposeDbContextAsync();
    
    Task<ServiceBaseUrl?> Get(string url);
    
    Task<ServiceBaseUrl?> Get();

    ServiceBaseUrl Update(ServiceBaseUrl serviceBaseUrl);
    
    ServiceBaseUrl Add(ServiceBaseUrl serviceBaseUrl);
    
}