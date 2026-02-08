using Application.DTOs.Analytics;
using Application.DTOs.Notifications;
using Application.Interfaces;
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

    public AnalyticsController(IAnalyticsService analyticsService, ITeamService teamService)
    {
        _analyticsService = analyticsService;
        _teamService = teamService;
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
        
        if (!isAdmin) return Forbid();

        var overview = await _analyticsService.GetOverviewAsync();
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

