using System.ComponentModel.DataAnnotations;

namespace Abm.Pyro.Domain.Configuration;

public class ResourceEndpointTenant
{
    [Required(AllowEmptyStrings=false, ErrorMessage = "A ResourceEndpointPolicies.DefaultPolicy must be provided")]
    public required string TenantCode { get; init; }
    
    [Required(AllowEmptyStrings=false, ErrorMessage = "A ResourceEndpointPolicies.DefaultPolicy must be provided")]
    public required string DefaultPolicy { get; init; }
    
    public required IEnumerable<ResourceEndpointPolicyMap> Enforce { get; init; } = new List<ResourceEndpointPolicyMap>();
    
}