using Application.DTOs.Analytics;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ITeamService _teamService;
    private readonly ActivityLogMigrationService _migrationService;

    public AnalyticsController(
        IAnalyticsService analyticsService, 
        ITeamService teamService,
        ActivityLogMigrationService migrationService)
    {
        _analyticsService = analyticsService;
        _teamService = teamService;
        _migrationService = migrationService;
    }

    [HttpPost("migrate-logs")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> MigrateLogs()
    {
        await _migrationService.MigrateLegacyLogsAsync();
        return Ok(new { message = "تمت عملية تحديث سجلات النشاط بنجاح" });
    }

    [HttpGet("overview")]
    [Authorize]
    public async Task<ActionResult> GetOverview([FromQuery] Guid? teamId = null)
    {
        var isAdmin = User.IsInRole("Admin");

        if (teamId.HasValue)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            var userId = Guid.Parse(userIdStr);

            // Security Check: If not admin, must own the team
            if (!isAdmin)
            {
                var team = await _teamService.GetByIdAsync(teamId.Value);
                if (team == null) return NotFound();

                if (team.CaptainId != userId)
                {
                    return Forbid();
                }
            }
            
            var teamAnalytics = await _analyticsService.GetTeamAnalyticsAsync(teamId.Value);
            return Ok(teamAnalytics);
        }
        
        var isCreator = User.IsInRole("TournamentCreator");
        if (!isAdmin && !isCreator) return Forbid();

        Guid? creatorId = isCreator ? Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!) : null;
        var overview = await _analyticsService.GetOverviewAsync(creatorId);
        return Ok(overview);
    }

    [HttpGet("activities")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetRecentActivity()
    {
        var activity = await _analyticsService.GetRecentActivitiesAsync();
        return Ok(activity);
    }
}

