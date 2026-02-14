using System.Threading;
using Application.DTOs.Settings;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Domain.Interfaces;

namespace Application.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly IRepository<SystemSetting> _settingsRepository;
    private readonly IAnalyticsService _analyticsService;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private const string CacheKey = "SystemSettings_Global";

    public SystemSettingsService(
        IRepository<SystemSetting> settingsRepository,
        IAnalyticsService analyticsService,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _settingsRepository = settingsRepository;
        _analyticsService = analyticsService;
        _cache = cache;
    }

    public async Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await GetCachedSettingsAsync(ct);
        return new SystemSettingsDto
        {
            AllowTeamCreation = settings.AllowTeamCreation,
            MaintenanceMode = settings.MaintenanceMode,
            UpdatedAt = settings.UpdatedAt
        };
    }

    public async Task<SystemSettingsDto> UpdateSettingsAsync(SystemSettingsDto dto, Guid adminId, CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsAsync(ct);

        settings.AllowTeamCreation = dto.AllowTeamCreation;
        settings.MaintenanceMode = dto.MaintenanceMode;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = adminId;

        await _settingsRepository.UpdateAsync(settings, ct);
        
        // Invalidate Cache
        _cache.Remove(CacheKey);
        
        return new SystemSettingsDto
        {
            AllowTeamCreation = settings.AllowTeamCreation,
            MaintenanceMode = settings.MaintenanceMode,
            UpdatedAt = settings.UpdatedAt
        };
    }

    public async Task<bool> IsTeamCreationAllowedAsync(CancellationToken ct = default)
    {
        var settings = await GetCachedSettingsAsync(ct);
        return settings.AllowTeamCreation;
    }

    public async Task<bool> IsMaintenanceModeEnabledAsync(CancellationToken ct = default)
    {
        var settings = await GetCachedSettingsAsync(ct);
        return settings.MaintenanceMode;
    }

    private async Task<SystemSetting> GetCachedSettingsAsync(CancellationToken ct = default)
    {
        if (!_cache.TryGetValue(CacheKey, out SystemSetting settings))
        {
            settings = await GetOrCreateSettingsAsync(ct);
            var options = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));
            
            _cache.Set(CacheKey, settings, options);
        }
        return settings!;
    }

    /// <summary>
    /// Gets the settings or creates default settings if none exist.
    /// </summary>
    private async Task<SystemSetting> GetOrCreateSettingsAsync(CancellationToken ct = default)
    {
        var allSettings = (await _settingsRepository.GetAllAsync(ct))
            .OrderBy(s => s.CreatedAt)
            .ToList();
            
        // DATA INTEGRITY: If somehow multiple rows were created, keep only the first one
        if (allSettings.Count > 1)
        {
            Console.Out.WriteLine($"[WARNING] Multiple SystemSettings detected ({allSettings.Count}). Cleaning up...");
            for (int i = 1; i < allSettings.Count; i++)
            {
                await _settingsRepository.DeleteAsync(allSettings[i], ct);
            }
        }

        var settings = allSettings.FirstOrDefault();

        if (settings == null)
        {
            settings = new SystemSetting
            {
                AllowTeamCreation = true,
                MaintenanceMode = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _settingsRepository.AddAsync(settings, ct);
        }

        return settings;
    }
}
