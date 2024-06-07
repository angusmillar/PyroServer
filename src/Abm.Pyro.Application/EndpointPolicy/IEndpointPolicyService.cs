using Abm.Pyro.Domain.Enums;

namespace Abm.Pyro.Application.EndpointPolicy;

public interface IEndpointPolicyService
{
    bool? ValidateConfiguration(CancellationToken cancellationToken);
    void PrimeEndpointPolicies();
    EndpointPolicy GetEndpointPolicy(string endpointName);
    EndpointPolicy GetDefaultEndpointPolicy();
}