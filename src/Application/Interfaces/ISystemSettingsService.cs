using System.Threading;
using Application.DTOs.Settings;

namespace Application.Interfaces;

public interface ISystemSettingsService
{
    /// <summary>
    /// Gets the current system settings.
    /// </summary>
    Task<SystemSettingsDto> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the system settings. Admin only.
    /// </summary>
    Task<SystemSettingsDto> UpdateSettingsAsync(SystemSettingsDto settings, Guid adminId, CancellationToken ct = default);

    /// <summary>
    /// Checks if team creation is currently allowed.
    /// Used by TeamService before creating a team.
    /// </summary>
    Task<bool> IsTeamCreationAllowedAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if maintenance mode is enabled.
    /// Used by middleware to block non-admin access.
    /// </summary>
    Task<bool> IsMaintenanceModeEnabledAsync(CancellationToken ct = default);
}
