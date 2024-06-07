namespace Abm.Pyro.Domain.Configuration;

public class ResourceEndpointPolicy
{
    public required string PolicyCode { get; init; }
    public string? PolicyDescription { get; init; }
    public required bool AllowCreate { get; init; } = false;
    public required bool AllowRead { get; init; } = false;
    public required bool AllowUpdate { get; init; } = false;
    public required bool AllowDelete { get; init; } = false;
    public required bool AllowSearch { get; init; } = false; 
    public required bool AllowVersionRead { get; init; } = false;
    public required bool AllowHistory { get; init; } = false;
    public required bool AllowConditionalCreate { get; init; } = false;
    public required bool AllowConditionalUpdate { get; init; } = false;
    public required bool AllowConditionalDelete { get; init; } = false;
    public required bool AllowBaseTransaction { get; init; } = false;
    public required bool AllowBaseBatch { get; init; } = false;
    public required bool AllowBaseMetadata { get; init; } = false;
    public required bool AllowBaseHistory { get; init; } = false;
    
}