namespace Abm.Pyro.Application.EndpointPolicy;

public class EndpointPolicyRules : IEndpointPolicyRules
{
    public required Dictionary<string, TenantEndpointPolicyRules> AllTenantEndpointPolicyDictionary { get; init; } = new();
    public bool IsEndpointPolicyConfigurationValid { get; set; } = false;
}