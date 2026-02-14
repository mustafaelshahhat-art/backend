using Application.DTOs.Settings;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/admin/[controller]")]
[Authorize(Policy = "RequireAdmin")]
public class SystemSettingsController : ControllerBase
{
    private readonly ISystemSettingsService _settingsService;

    public SystemSettingsController(ISystemSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Gets the current system settings.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SystemSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Updates the system settings.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SystemSettingsDto>> UpdateSettings(SystemSettingsDto request, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(adminId) || !Guid.TryParse(adminId, out var adminGuid))
        {
            return Unauthorized();
        }

        var settings = await _settingsService.UpdateSettingsAsync(request, adminGuid, cancellationToken);
        return Ok(settings);
    }

    /// <summary>
    /// Public endpoint to check if maintenance mode is enabled.
    /// Used by frontend to show maintenance message.
    /// </summary>
    [HttpGet("maintenance-status")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> GetMaintenanceStatus(CancellationToken cancellationToken)
    {
        var isMaintenanceMode = await _settingsService.IsMaintenanceModeEnabledAsync(cancellationToken);
        return Ok(new { maintenanceMode = isMaintenanceMode });
    }
}
