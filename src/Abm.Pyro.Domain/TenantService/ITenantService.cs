namespace Abm.Pyro.Domain.TenantService;

public interface ITenantService
{
    public string GetScopedTenantCode();
    public void SetScopedTenant(Configuration.Tenant tenant);
    public Domain.Configuration.Tenant GetScopedTenant();
    public IReadOnlyCollection<Configuration.Tenant> GetTenantList();
    
}