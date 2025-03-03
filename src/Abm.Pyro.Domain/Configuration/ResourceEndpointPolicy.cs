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
    
    /// <summary>
    /// A list if allowed $Operations for the system's base endpoint.
    /// Example: ["validate", "versions"]
    /// Note that for AllowBaseOperations, where a Resource Type endpoint policy is set to override the default, it will be ignored.
    /// This is because it makes no logical sense, only the default AllowBaseOperations is applied because there is only the one
    /// endpoint at the system base
    /// </summary>
    public List<string>? AllowBaseOperations { get; init; }
    
    /// <summary>
    /// A list if allowed $Operations for the Resource Type endpoint.
    /// Example: ["validate", "lookup"]
    /// Note that for AllowResourceTypeOperations, where a Resource Type endpoint policy is set to override the default.
    /// That policy will override the default policy, the default policy is ignored.
    /// However, where there are many AllowResourceTypeOperations policies in play on the same Resource Type endpoint,
    /// the union of them will apply.
    /// </summary>
    public List<string>? AllowResourceTypeOperations { get; init; }
    
    /// <summary>
    /// A list if allowed $Operations for the Resource Type endpoint.
    /// Example: ["validate", "versions", "lookup"]
    /// Note that for AllowResourceInstanceOperations, where a Resource Type endpoint policy is set to override the default.
    /// That policy will override the default policy, the default policy is ignored.
    /// However, where there are many AllowResourceTypeOperations policies in play on the same Resource Type endpoint,
    /// the union of them will apply.
    /// </summary>
    public List<string>? AllowResourceInstanceOperations { get; init; }
    
}