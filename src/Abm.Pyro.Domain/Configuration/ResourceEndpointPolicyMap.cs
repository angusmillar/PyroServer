namespace Abm.Pyro.Domain.Configuration;

public class ResourceEndpointPolicyMap
{
    public required IEnumerable<string> Endpoints { get; init; } = new List<string>();
    public required IEnumerable<string> Policies { get; init; } = new List<string>();
    
}