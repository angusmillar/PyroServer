namespace Abm.Pyro.Application.EndpointPolicy;

public interface IEndpointPolicyRules
{
    Dictionary<string, TenantEndpointPolicyRules> AllTenantEndpointPolicyDictionary { get; init; } 
    bool IsEndpointPolicyConfigurationValid { get; set; }
}