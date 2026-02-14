using Application.DTOs.Analytics;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Application.Services;
using Domain.Enums;
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
    public async Task<ActionResult> MigrateLogs(CancellationToken cancellationToken)
    {
        await _migrationService.MigrateLegacyLogsAsync(cancellationToken);
        return Ok(new { message = "تمت عملية تحديث سجلات النشاط بنجاح" });
    }

    [HttpGet("overview")]
    [Authorize]
    public async Task<ActionResult> GetOverview([FromQuery] Guid? teamId = null, CancellationToken cancellationToken = default)
    {
        var (userId, userRole) = GetUserContext();
        var isAdmin = userRole == UserRole.Admin.ToString();

        if (teamId.HasValue)
        {
            // Security Check: If not admin, must own the team
            if (!isAdmin)
            {
                var team = await _teamService.GetByIdAsync(teamId.Value, cancellationToken);
                if (team == null) return NotFound();
                
                // Check if user is the captain within the Players list
                if (!team.Players.Any(p => p.UserId == userId && p.TeamRole == TeamRole.Captain))
                {
                    return Forbid();
                }
            }
            
            var teamAnalytics = await _analyticsService.GetTeamAnalyticsAsync(teamId.Value, cancellationToken);
            return Ok(teamAnalytics);
        }
        
        var isCreator = userRole == UserRole.TournamentCreator.ToString();
        if (!isAdmin && !isCreator) return Forbid();

        Guid? creatorId = isCreator ? userId : null;
        var overview = await _analyticsService.GetOverviewAsync(creatorId, cancellationToken);
        return Ok(overview);
    }

    [HttpGet("activities")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetRecentActivity(CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var isAdmin = userRole == UserRole.Admin.ToString();
        var isCreator = userRole == UserRole.TournamentCreator.ToString();
        
        if (!isAdmin && !isCreator) return Forbid();

        Guid? creatorId = isCreator ? userId : null;
        var activity = await _analyticsService.GetRecentActivitiesAsync(creatorId, cancellationToken);
        return Ok(activity);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }
}
