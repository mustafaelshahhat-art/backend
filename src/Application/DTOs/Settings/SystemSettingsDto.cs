namespace Application.DTOs.Settings;

/// <summary>
/// DTO for system settings response and update request.
/// </summary>
public class SystemSettingsDto
{
    /// <summary>
    /// Controls whether players can create new teams.
    /// </summary>
    public bool AllowTeamCreation { get; set; }

    /// <summary>
    /// When enabled, only admins can access the system.
    /// </summary>
    public bool MaintenanceMode { get; set; }

    public DateTime UpdatedAt { get; set; }
}
