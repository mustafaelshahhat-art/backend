using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Public status endpoints — no authentication required.
/// Separated from admin controllers so pre-auth pages (login, landing) can safely call them.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class StatusController : ControllerBase
{
    private readonly ISystemSettingsService _settingsService;

    public StatusController(ISystemSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Public endpoint to check if maintenance mode is enabled.
    /// Called by frontend login/landing pages before authentication.
    /// </summary>
    [HttpGet("maintenance")]
    public async Task<ActionResult<object>> GetMaintenanceStatus(CancellationToken cancellationToken)
    {
        var isMaintenanceMode = await _settingsService.IsMaintenanceModeEnabledAsync(cancellationToken);
        return Ok(new { maintenanceMode = isMaintenanceMode });
    }
}
