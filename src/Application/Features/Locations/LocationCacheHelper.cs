using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Application.Features.Locations;

public static class LocationCacheHelper
{
    public const string CacheKeyGovernorates = "loc:governorates";
    public const string CacheKeyCities = "loc:cities:";
    public const string CacheKeyAreas = "loc:areas:";
    public static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    public static async Task<T?> GetFromCacheAsync<T>(
        IDistributedCache cache, ILogger logger, string key, CancellationToken ct) where T : class
    {
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is null || bytes.Length == 0) return null;
            return System.Text.Json.JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for key {Key}", key);
            return null;
        }
    }

    public static async Task SetCacheAsync<T>(
        IDistributedCache cache, ILogger logger, string key, T value, TimeSpan duration, CancellationToken ct)
    {
        try
        {
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value);
            await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = duration
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for key {Key}", key);
        }
    }

    public static async Task InvalidateGovernorateCacheAsync(
        IDistributedCache cache, ILogger logger, CancellationToken ct)
    {
        try { await cache.RemoveAsync(CacheKeyGovernorates, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to invalidate governorate cache"); }
    }

    public static async Task InvalidateCityCacheAsync(
        IDistributedCache cache, ILogger logger, Guid governorateId, CancellationToken ct)
    {
        try { await cache.RemoveAsync(CacheKeyCities + governorateId, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to invalidate city cache for governorate {GovernorateId}", governorateId); }
    }

    public static async Task InvalidateAreaCacheAsync(
        IDistributedCache cache, ILogger logger, Guid cityId, CancellationToken ct)
    {
        try { await cache.RemoveAsync(CacheKeyAreas + cityId, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to invalidate area cache for city {CityId}", cityId); }
    }
}
