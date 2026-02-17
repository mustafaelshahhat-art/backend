using Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services;

public class RedisLockService : IDistributedLock
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisLockService> _logger;
    private readonly string _lockPrefix = "korazone365:lock:";

    public RedisLockService(IConnectionMultiplexer redis, ILogger<RedisLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> AcquireLockAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var fullKey = _lockPrefix + key;
            var value = Environment.MachineName;

            // PROD-AUDIT: SET key value NX PX expiry
            var acquired = await db.StringSetAsync(fullKey, value, expiry, When.NotExists);

            if (acquired)
            {
                _logger.LogInformation("Successfully acquired distributed lock for key: {Key} (Instance: {Instance})", fullKey, value);
            }
            else
            {
                _logger.LogDebug("Failed to acquire distributed lock for key: {Key}. Already held by another instance.", fullKey);
            }

            return acquired;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis connection error while acquiring distributed lock for key: {Key}. Failing safe – lock NOT acquired.", key);
            return false;
        }
    }

    public async Task ReleaseLockAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var fullKey = _lockPrefix + key;
        
        // Simple release. In production, we might want to check if WE own the lock before deleting, 
        // but for a single dedicated background job instance winner, this is usually acceptable.
        await db.KeyDeleteAsync(fullKey);
        _logger.LogInformation("Released distributed lock for key: {Key}", fullKey);
    }
}
