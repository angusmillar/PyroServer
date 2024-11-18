using System.ComponentModel.DataAnnotations;

namespace Abm.Pyro.Domain.Configuration;

public sealed class ResourceEndpointPoliciesSettings
{
  public const string SectionName = "ResourceEndpointPolicies";
  
  public required IEnumerable<ResourceEndpointTenant> Tenants { get; init; } = new List<ResourceEndpointTenant>();
    
  public required IEnumerable<ResourceEndpointPolicy> Policies { get; init; } = new List<ResourceEndpointPolicy>();

}
