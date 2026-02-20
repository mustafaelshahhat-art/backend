using System.Text.Json;
using System.Threading;
using Application.DTOs.Settings;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Domain.Interfaces;

namespace Application.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly IRepository<SystemSetting> _settingsRepository;
    private readonly IDistributedCache _cache;
    private const string CacheKey = "SystemSettings_Global";

    public SystemSettingsService(
        IRepository<SystemSetting> settingsRepository,
        IDistributedCache cache)
    {
        _settingsRepository = settingsRepository;
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
        await _cache.RemoveAsync(CacheKey, ct);
        
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
        var cached = await _cache.GetStringAsync(CacheKey, ct);
        if (cached != null)
        {
            var settings = JsonSerializer.Deserialize<SystemSetting>(cached);
            if (settings != null) return settings;
        }

        var freshSettings = await GetOrCreateSettingsAsync(ct);
        
        var options = new DistributedCacheEntryOptions()
            .SetSlidingExpiration(TimeSpan.FromMinutes(5))
            .SetAbsoluteExpiration(TimeSpan.FromHours(1));
        
        var json = JsonSerializer.Serialize(freshSettings);
        await _cache.SetStringAsync(CacheKey, json, options, ct);
        
        return freshSettings;
    }

    /// <summary>
    /// Gets the settings or creates default settings if none exist.
    /// </summary>
    private async Task<SystemSetting> GetOrCreateSettingsAsync(CancellationToken ct = default)
    {
        var result = await _settingsRepository.GetPagedAsync(1, 10, null, q => q.OrderBy(s => s.CreatedAt), ct);
        var allSettings = result.Items.ToList();
            
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
