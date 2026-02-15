using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;

namespace Infrastructure.Services;

public class UserCacheService : IUserCacheService
{
    private readonly IDistributedCache _cache;
    private const string CacheKeyPrefix = "user_cache:";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    public UserCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<UserCacheModel?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        var cachedData = await _cache.GetStringAsync(cacheKey, ct);
        
        if (string.IsNullOrEmpty(cachedData))
        {
            return null;
        }

        return JsonSerializer.Deserialize<UserCacheModel>(cachedData);
    }

    public async Task SetUserAsync(Guid userId, User user, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        var model = new UserCacheModel
        {
            TokenVersion = user.TokenVersion,
            Status = user.Status,
            Name = user.Name,
            Role = user.Role.ToString()
        };

        var jsonData = JsonSerializer.Serialize(model);
        await _cache.SetStringAsync(cacheKey, jsonData, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultExpiration
        }, ct);
    }

    public async Task InvalidateUserAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}";
        await _cache.RemoveAsync(cacheKey, ct);
    }
}
