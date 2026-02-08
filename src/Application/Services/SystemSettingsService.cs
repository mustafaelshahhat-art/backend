using Application.DTOs.Settings;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private readonly IRepository<SystemSetting> _settingsRepository;
    private readonly IAnalyticsService _analyticsService;

    public SystemSettingsService(
        IRepository<SystemSetting> settingsRepository,
        IAnalyticsService analyticsService)
    {
        _settingsRepository = settingsRepository;
        _analyticsService = analyticsService;
    }

    public async Task<SystemSettingsDto> GetSettingsAsync()
    {
        var settings = await GetOrCreateSettingsAsync();
        return new SystemSettingsDto
        {
            AllowTeamCreation = settings.AllowTeamCreation,
            MaintenanceMode = settings.MaintenanceMode,
            UpdatedAt = settings.UpdatedAt
        };
    }

    public async Task<SystemSettingsDto> UpdateSettingsAsync(SystemSettingsDto dto, Guid adminId)
    {
        var settings = await GetOrCreateSettingsAsync();

        var logMsg = $"[{DateTime.UtcNow}] Updating Settings. ID: {settings.Id}, New Team={dto.AllowTeamCreation}, New Maint={dto.MaintenanceMode}{Environment.NewLine}";
        System.IO.File.AppendAllText("settings_debug.log", logMsg);

        settings.AllowTeamCreation = dto.AllowTeamCreation;
        settings.MaintenanceMode = dto.MaintenanceMode;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByAdminId = adminId;

        await _settingsRepository.UpdateAsync(settings);
        
        return new SystemSettingsDto
        {
            AllowTeamCreation = settings.AllowTeamCreation,
            MaintenanceMode = settings.MaintenanceMode,
            UpdatedAt = settings.UpdatedAt
        };
    }

    public async Task<bool> IsTeamCreationAllowedAsync()
    {
        var settings = await GetOrCreateSettingsAsync();
        return settings.AllowTeamCreation;
    }

    public async Task<bool> IsMaintenanceModeEnabledAsync()
    {
        var settings = await GetOrCreateSettingsAsync();
        return settings.MaintenanceMode;
    }

    /// <summary>
    /// Gets the settings or creates default settings if none exist.
    /// </summary>
    private async Task<SystemSetting> GetOrCreateSettingsAsync()
    {
        var allSettings = (await _settingsRepository.GetAllAsync())
            .OrderBy(s => s.CreatedAt)
            .ToList();
            
        // DATA INTEGRITY: If somehow multiple rows were created, keep only the first one
        if (allSettings.Count > 1)
        {
            Console.Out.WriteLine($"[WARNING] Multiple SystemSettings detected ({allSettings.Count}). Cleaning up...");
            for (int i = 1; i < allSettings.Count; i++)
            {
                await _settingsRepository.DeleteAsync(allSettings[i]);
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
            await _settingsRepository.AddAsync(settings);
        }

        return settings;
    }
}
