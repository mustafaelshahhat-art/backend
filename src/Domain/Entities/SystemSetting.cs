namespace Domain.Entities;

/// <summary>
/// Stores system-wide configuration settings.
/// This is a single-row table that stores all system settings.
/// </summary>
public class SystemSetting : BaseEntity
{
    /// <summary>
    /// Controls whether players can create new teams.
    /// When false, team creation is blocked system-wide.
    /// </summary>
    public bool AllowTeamCreation { get; set; } = true;

    /// <summary>
    /// When enabled, only admins can access the system.
    /// Login, Register, and all public endpoints are blocked.
    /// </summary>
    public bool MaintenanceMode { get; set; } = false;

    public Guid? UpdatedByAdminId { get; set; }
}
