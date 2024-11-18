namespace Abm.Pyro.Application.EndpointPolicy;

public record TenantEndpointPolicyRules(
    string TenantCode,
    EndpointPolicy DefaultPolicy,
    Dictionary<string, EndpointPolicy> EndpointPolicyDictionary);