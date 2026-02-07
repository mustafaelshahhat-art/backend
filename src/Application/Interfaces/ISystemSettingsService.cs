using Application.DTOs.Settings;

namespace Application.Interfaces;

public interface ISystemSettingsService
{
    /// <summary>
    /// Gets the current system settings.
    /// </summary>
    Task<SystemSettingsDto> GetSettingsAsync();

    /// <summary>
    /// Updates the system settings. Admin only.
    /// </summary>
    Task<SystemSettingsDto> UpdateSettingsAsync(SystemSettingsDto settings, Guid adminId);

    /// <summary>
    /// Checks if team creation is currently allowed.
    /// Used by TeamService before creating a team.
    /// </summary>
    Task<bool> IsTeamCreationAllowedAsync();

    /// <summary>
    /// Checks if maintenance mode is enabled.
    /// Used by middleware to block non-admin access.
    /// </summary>
    Task<bool> IsMaintenanceModeEnabledAsync();
}
