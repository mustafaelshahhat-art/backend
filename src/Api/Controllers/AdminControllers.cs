using Application.DTOs.Analytics;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")] // Analytics for Admin mostly
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverview>> GetOverview()
    {
        var overview = await _analyticsService.GetOverviewAsync();
        return Ok(overview);
    }

    [HttpGet("activities")]
    public async Task<ActionResult<IEnumerable<ActivityDto>>> GetRecentActivity()
    {
        var activity = await _analyticsService.GetRecentActivitiesAsync();
        return Ok(activity);
    }
}

