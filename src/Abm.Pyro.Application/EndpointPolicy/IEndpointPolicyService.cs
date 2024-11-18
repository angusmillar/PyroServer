using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.EndpointPolicy;

public interface IEndpointPolicyService
{
    bool ValidateConfiguration(string tenantCode, CancellationToken cancellationToken);
    void PrimeEndpointPolicies(string tenantCode);
    EndpointPolicy GetEndpointPolicy(string tenantCode, string endpointName);
    EndpointPolicy GetDefaultEndpointPolicy(string tenantCode);
}