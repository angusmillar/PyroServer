using System.ComponentModel.DataAnnotations;

namespace Abm.Pyro.Domain.Configuration;

public sealed class ResourceEndpointPolicySettings
{
  public const string SectionName = "ResourceEndpointPolicies";
  
  [Required(AllowEmptyStrings=false, ErrorMessage = "A ResourceEndpointPolicies.DefaultPolicy must be provided")]
  public required string DefaultPolicy { get; init; }
  public required IEnumerable<ResourceEndpointPolicyMap> Enforce { get; init; } = new List<ResourceEndpointPolicyMap>();
  public required IEnumerable<ResourceEndpointPolicy> Policies { get; init; } = new List<ResourceEndpointPolicy>();

}
