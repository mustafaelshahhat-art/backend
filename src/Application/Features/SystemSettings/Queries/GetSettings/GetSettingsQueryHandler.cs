using System.Text.Json;
using Application.DTOs.Settings;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace Application.Features.SystemSettings.Queries.GetSettings;

public class GetSettingsQueryHandler : IRequestHandler<GetSettingsQuery, SystemSettingsDto>
{
    private readonly IRepository<SystemSetting> _settingsRepository;
    private readonly IDistributedCache _cache;
    private const string CacheKey = "SystemSettings_Global";

    public GetSettingsQueryHandler(IRepository<SystemSetting> settingsRepository, IDistributedCache cache)
    {
        _settingsRepository = settingsRepository;
        _cache = cache;
    }

    public async Task<SystemSettingsDto> Handle(GetSettingsQuery request, CancellationToken ct)
    {
        var settings = await GetCachedSettingsAsync(ct);
        return new SystemSettingsDto
        {
            AllowTeamCreation = settings.AllowTeamCreation,
            MaintenanceMode = settings.MaintenanceMode,
            UpdatedAt = settings.UpdatedAt
        };
    }

    private async Task<SystemSetting> GetCachedSettingsAsync(CancellationToken ct)
    {
        var cached = await _cache.GetStringAsync(CacheKey, ct);
        if (cached != null)
        {
            var settings = JsonSerializer.Deserialize<SystemSetting>(cached);
            if (settings != null) return settings;
        }

        var result = await _settingsRepository.GetPagedAsync(1, 10, null, q => q.OrderBy(s => s.CreatedAt), ct);
        var freshSettings = result.Items.FirstOrDefault();

        if (freshSettings == null)
        {
            freshSettings = new SystemSetting
            {
                AllowTeamCreation = true,
                MaintenanceMode = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _settingsRepository.AddAsync(freshSettings, ct);
        }

        var options = new DistributedCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(5))
            .SetAbsoluteExpiration(TimeSpan.FromHours(1));
        var json = JsonSerializer.Serialize(freshSettings);
        await _cache.SetStringAsync(CacheKey, json, options, ct);

        return freshSettings;
    }
}
