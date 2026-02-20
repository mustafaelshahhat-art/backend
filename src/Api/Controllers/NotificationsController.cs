using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Contracts.Notifications.Responses;
using MediatR;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NotificationDto = Application.DTOs.Notifications.NotificationDto;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null ? Guid.Parse(claim) : null;
    }

    /// <summary>GET /api/v1/notifications?page=1&amp;pageSize=20</summary>
    [HttpGet]
    public async Task<ActionResult<Application.Common.Models.PagedResult<NotificationDto>>> GetMyNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (pageSize > 100) pageSize = 100;

        var query = new Application.Features.Notifications.Queries.GetUserNotifications.GetUserNotificationsQuery(userId.Value, page, pageSize);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>GET /api/v1/notifications/unread-count</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = new Application.Features.Notifications.Queries.GetUnreadCount.GetUnreadCountQuery(userId.Value);
        var count = await _mediator.Send(query, ct);
        return Ok(new UnreadCountResponse(count));
    }

    /// <summary>POST /api/v1/notifications/{id}/read</summary>
    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new Application.Features.Notifications.Commands.MarkAsRead.MarkAsReadCommand(id, userId.Value);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    /// <summary>POST /api/v1/notifications/read-all</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new Application.Features.Notifications.Commands.MarkAllAsRead.MarkAllAsReadCommand(userId.Value);
        await _mediator.Send(command, ct);
        return NoContent();
    }

    /// <summary>DELETE /api/v1/notifications/{id}</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var command = new Application.Features.Notifications.Commands.DeleteNotification.DeleteNotificationCommand(id, userId.Value);
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
