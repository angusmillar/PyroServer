using System.Text.Json;
using Abm.Pyro.Domain.Configuration;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Abm.Pyro.Api.Extensions;

public static class FusionCacheServiceCollectionExtension
{
    public static IServiceCollection AddFusionCaching(this IServiceCollection services, RedisCacheSettings redisCacheSettings)
    {
        var fusionCacheBuilder = services.AddFusionCache();
        fusionCacheBuilder.WithOptions(options =>
            {
                options.DistributedCacheCircuitBreakerDuration = TimeSpan.FromSeconds(2);
            })
            .WithDefaultEntryOptions(new FusionCacheEntryOptions()
            {
                //The default expiry for all cached items
                Duration = redisCacheSettings.LocalCacheExpiryTimeSpan,
                DistributedCacheDuration = redisCacheSettings.RedisCacheExpiryTimeSpan,
                JitterMaxDuration = TimeSpan.FromSeconds(2),
                    
                // FAIL-SAFE OPTIONS (The ability to use a stale cached item where the refresh from the DB is slow)
                
                //If you are to enable this 'IsFailSafeEnabled' then you must ensure that all factory services that
                //refresh the value from the database, do so by creating their own standalone service container scope.
                //This would be done by injected a IServiceScopeFactory and then resolving their own DbContext, and any
                //other required services to do the work. This is because the factory will be run on a background thread
                //by FusionCache in a different scope to the web request scope, and therefore any services scoped by the
                //web request, will have been disposed before the background thread needs to use then.  
                
                IsFailSafeEnabled = false,
                // FailSafeMaxDuration = TimeSpan.FromHours(1),
                // FailSafeThrottleDuration = TimeSpan.FromSeconds(25),
                //
                // // FACTORY TIMEOUTS
                // FactorySoftTimeout = TimeSpan.FromMilliseconds(1),
                // FactoryHardTimeout = TimeSpan.FromSeconds(2),
                //
                // // DISTRIBUTED CACHE OPTIONS
                // DistributedCacheSoftTimeout = TimeSpan.FromSeconds(1),
                // DistributedCacheHardTimeout = TimeSpan.FromSeconds(2),
                //
                // AllowBackgroundDistributedCacheOperations = false, 
                
            })
            .AsHybridCache();


        if (redisCacheSettings.UseRedisCache)
        {
            fusionCacheBuilder.WithSerializer(new FusionCacheSystemTextJsonSerializer(
                    new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector)))
                .WithDistributedCache(
                    new RedisCache(new RedisCacheOptions()
                    {
                        Configuration = redisCacheSettings.ConnectionString
                    }))
                .WithBackplane(
                    new RedisBackplane(new RedisBackplaneOptions()
                    {
                        Configuration = redisCacheSettings.ConnectionString
                    })
                );    
        }
        
        return services; 
    }
}