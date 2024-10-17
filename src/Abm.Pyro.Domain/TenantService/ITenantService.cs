namespace Abm.Pyro.Domain.TenantService;

public interface ITenantService
{
    public void SetScopedTenant(Domain.Configuration.Tenant tenant);
    public Domain.Configuration.Tenant GetScopedTenant();
    public IReadOnlyCollection<Domain.Configuration.Tenant> GetTenantList();
}