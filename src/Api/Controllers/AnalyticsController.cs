using Application.Contracts.Common;
using Application.DTOs.Analytics;
using Application.DTOs.Notifications;
using Application.Features.Analytics.Commands.MigrateLogs;
using Application.Features.Analytics.Queries.GetActivities;
using Application.Features.Analytics.Queries.GetAnalyticsOverview;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("migrate-logs")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<MessageResponse>> MigrateLogs(CancellationToken cancellationToken)
    {
        await _mediator.Send(new MigrateLogsCommand(), cancellationToken);
        return Ok(new MessageResponse("تمت عملية تحديث سجلات النشاط بنجاح"));
    }

    [HttpGet("overview")]
    [Authorize]
    [OutputCache(PolicyName = "Analytics")]
    [ProducesResponseType(typeof(AnalyticsOverview), 200)]
    [ProducesResponseType(typeof(TeamAnalyticsDto), 200)]
    public async Task<IActionResult> GetOverview([FromQuery] Guid? teamId = null, CancellationToken cancellationToken = default)
    {
        var (userId, userRole) = GetUserContext();
        var query = new GetAnalyticsOverviewQuery(teamId, userId, userRole);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("activities")]
    [Authorize]
    public async Task<ActionResult<Application.Common.Models.PagedResult<ActivityDto>>> GetRecentActivity(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? actorRole = null,
        [FromQuery] string? actionType = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int? minSeverity = null,
        [FromQuery] Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var (currentUserId, userRole) = GetUserContext();
        var query = new GetActivitiesQuery(
            page, pageSize, actorRole, actionType, entityType,
            fromDate, toDate, minSeverity, userId,
            currentUserId, userRole);
        var activity = await _mediator.Send(query, cancellationToken);
        return Ok(activity);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }
}
