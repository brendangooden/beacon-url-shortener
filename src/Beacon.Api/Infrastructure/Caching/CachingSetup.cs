using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Beacon.Api.Infrastructure.Caching;

public static class CachingSetup
{
    private const string RedisResourceName = "cache";

    /// <summary>
    /// Redis client + distributed cache (Aspire), then FusionCache: L1 memory + L2 Redis + a Redis
    /// backplane so every API instance stays coherent when a link changes on one of them.
    /// </summary>
    public static WebApplicationBuilder AddAppCaching(this WebApplicationBuilder builder)
    {
        // IConnectionMultiplexer (used by the backplane) + IDistributedCache (FusionCache L2).
        builder.AddRedisClient(RedisResourceName);
        builder.AddRedisDistributedCache(RedisResourceName);

        var fusion = builder.Services.AddFusionCache()
            .WithDefaultEntryOptions(o =>
            {
                o.Duration = TimeSpan.FromHours(1);
                o.IsFailSafeEnabled = true;                       // serve stale on a DB/Redis blip
                o.FailSafeMaxDuration = TimeSpan.FromHours(6);
                o.FailSafeThrottleDuration = TimeSpan.FromSeconds(30);
                o.FactorySoftTimeout = TimeSpan.FromMilliseconds(200);
                o.FactoryHardTimeout = TimeSpan.FromSeconds(2);
                o.DistributedCacheSoftTimeout = TimeSpan.FromSeconds(1);
                o.DistributedCacheHardTimeout = TimeSpan.FromSeconds(2);
                o.AllowBackgroundDistributedCacheOperations = true;
                o.JitterMaxDuration = TimeSpan.FromSeconds(5);    // spread expiries, avoid stampede
            })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithRegisteredDistributedCache();

        var redisConnection = builder.Configuration.GetConnectionString(RedisResourceName);
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            fusion.WithStackExchangeRedisBackplane(o => o.Configuration = redisConnection);
        }

        builder.Services.AddScoped<LinkCache>();
        return builder;
    }
}
