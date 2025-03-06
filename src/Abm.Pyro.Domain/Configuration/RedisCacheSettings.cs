namespace Abm.Pyro.Domain.Configuration;

public class RedisCacheSettings
{
    public const string SectionName = "RedisCache";

    /// <summary>
    /// If set to false then no Redis instance is required and only the
    /// (Level 1) local service's memory cache is used.
    /// </summary>
    public bool UseRedisCache { get; init; } = false;
    
    /// <summary>
    /// The Redis connection string, required when UseRedisCache is True
    /// </summary>
    public string? ConnectionString { get; init; }
    
    /// <summary>
    /// When to remove the cached item from the (Level 1) local service's memory cache
    /// The LocalCacheExpiry must be less than the RedisCacheExpiry 
    /// </summary>
    public required TimeSpan LocalCacheExpiryTimeSpan { get; init; } = TimeSpan.FromMinutes(10);
    
    /// <summary>
    /// When to remove the cached item from the (Level 2) Redis cache
    /// The LocalCacheExpiry must be less than the RedisCacheExpiry 
    /// </summary>
    public required TimeSpan RedisCacheExpiryTimeSpan { get; init; } = TimeSpan.FromMinutes(30);
    
}