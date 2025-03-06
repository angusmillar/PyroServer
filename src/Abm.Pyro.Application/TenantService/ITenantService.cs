namespace Abm.Pyro.Application.TenantService;

public interface ITenantService
{
    public string GetScopedTenantCode();
    public void SetScopedTenant(Domain.Configuration.Tenant tenant);
    public Domain.Configuration.Tenant GetScopedTenant();
    public IReadOnlyCollection<Domain.Configuration.Tenant> GetTenantList();
    
}