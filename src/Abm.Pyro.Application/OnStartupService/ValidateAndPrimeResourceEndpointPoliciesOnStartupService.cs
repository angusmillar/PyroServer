using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.HostedServiceSupport;
using Abm.Pyro.Application.TenantService;
using Abm.Pyro.Domain.Configuration;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.OnStartupService;

public class ValidateAndPrimeResourceEndpointPoliciesOnStartupService(
    ILogger<FhirServiceBaseUrlManagementOnStartupService> logger,
    ITenantService tenantService,
    IEndpointPolicyService endpointPolicyService)
    : IAppStartupService
{
    public async Task DoWork(CancellationToken cancellationToken)
    {
        foreach (var tenant in tenantService.GetTenantList())
        {
            tenantService.SetScopedTenant(tenant);
            await ProcessTenant(tenant, cancellationToken);
        }
    }
    
    private Task ProcessTenant(Tenant tenant, CancellationToken cancellationToken)
    {
        bool isEndpointPolicyConfigurationValid = endpointPolicyService.ValidateConfiguration(tenantCode: tenant.Code, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }
        
        if (isEndpointPolicyConfigurationValid)
        {
            endpointPolicyService.PrimeEndpointPolicies(tenantCode: tenant.Code);
            logger.LogInformation("Tenant {Tenant} resource endpoint policies configuration is primed and valid", tenant.DisplayName);
        }
        else
        {
            logger.LogCritical("Tenant {Tenant} resource endpoint policies configuration is invalid", tenant.DisplayName);
        }
        
        return Task.CompletedTask;
    }

    
}