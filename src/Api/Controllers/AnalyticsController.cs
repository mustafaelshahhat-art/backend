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
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult> MigrateLogs()
    {
        await _migrationService.MigrateLegacyLogsAsync();
        return Ok(new { message = "تمت عملية تحديث سجلات النشاط بنجاح" });
    }

    [HttpGet("overview")]
    [Authorize]
    public async Task<ActionResult> GetOverview([FromQuery] Guid? teamId = null)
    {
        var (userId, userRole) = GetUserContext();
        var isAdmin = userRole == "Admin";

        if (teamId.HasValue)
        {
            // Security Check: If not admin, must own the team
            if (!isAdmin)
            {
                var team = await _teamService.GetByIdAsync(teamId.Value);
                if (team == null) return NotFound();
                
                // Note: TeamDto has CaptainId
                if (team.CaptainId != userId)
                {
                    return Forbid();
                }
            }
            
            var teamAnalytics = await _analyticsService.GetTeamAnalyticsAsync(teamId.Value);
            return Ok(teamAnalytics);
        }
        
        var isCreator = userRole == "TournamentCreator";
        if (!isAdmin && !isCreator) return Forbid();

        Guid? creatorId = isCreator ? userId : null;
        var overview = await _analyticsService.GetOverviewAsync(creatorId);
        return Ok(overview);
    }

    [HttpGet("activities")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetRecentActivity()
    {
        var (userId, userRole) = GetUserContext();
        var isAdmin = userRole == "Admin";
        var isCreator = userRole == "TournamentCreator";
        
        if (!isAdmin && !isCreator) return Forbid();

        Guid? creatorId = isCreator ? userId : null;
        var activity = await _analyticsService.GetRecentActivitiesAsync(creatorId);
        return Ok(activity);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.IsInRole("Admin") ? "Admin" : 
                   User.IsInRole("TournamentCreator") ? "TournamentCreator" : "Player";
        return (Guid.Parse(idStr!), role);
    }
}

