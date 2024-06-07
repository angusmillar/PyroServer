using Abm.Pyro.Application.EndpointPolicy;
using Abm.Pyro.Application.HostedServiceSupport;
using Microsoft.Extensions.Logging;

namespace Abm.Pyro.Application.OnStartupService;

public class ValidateAndPrimeResourceEndpointPoliciesOnStartupService(
    ILogger<FhirServiceBaseUrlManagementOnStartupService> logger,
    IEndpointPolicyService endpointPolicyService)
    : IAppStartupService
{
    public bool IsValid = true;
    public Task DoWork(CancellationToken cancellationToken)
    {
        bool? isEndpointPolicyConfigurationValid = endpointPolicyService.ValidateConfiguration(cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.CompletedTask;
        }
        
        ArgumentNullException.ThrowIfNull(isEndpointPolicyConfigurationValid);
        
        if (isEndpointPolicyConfigurationValid.Value)
        {
            endpointPolicyService.PrimeEndpointPolicies();
            logger.LogInformation("Resource Endpoint Policies Configuration is valid and primed");
        }
        else
        {
            logger.LogError("Resource Endpoint Policies Configuration is invalid");
        }
        
        return Task.CompletedTask;
    }

    
}