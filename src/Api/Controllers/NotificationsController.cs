using System;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
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

        var result = await _notificationService.GetUserNotificationsAsync(userId.Value, page, pageSize, null, null, ct);
        return Ok(result);
    }

    /// <summary>GET /api/v1/notifications/unread-count</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(userId.Value, ct);
        return Ok(new { count });
    }

    /// <summary>POST /api/v1/notifications/{id}/read</summary>
    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        await _notificationService.MarkAsReadAsync(id, userId.Value, ct);
        return NoContent();
    }

    /// <summary>POST /api/v1/notifications/read-all</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        await _notificationService.MarkAllAsReadAsync(userId.Value, ct);
        return NoContent();
    }

    /// <summary>DELETE /api/v1/notifications/{id}</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        await _notificationService.DeleteAsync(id, userId.Value, ct);
        return NoContent();
    }
}
