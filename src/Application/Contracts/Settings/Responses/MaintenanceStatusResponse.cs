namespace Application.Contracts.Settings.Responses;

/// <summary>
/// Maintenance status response.
/// Replaces anonymous { maintenanceMode } and { maintenanceMode, updatedAt }.
/// </summary>
public class MaintenanceStatusResponse
{
    public bool MaintenanceMode { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
