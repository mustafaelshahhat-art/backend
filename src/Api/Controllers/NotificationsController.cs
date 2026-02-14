using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Notification>>> GetMyNotifications(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var notifications = await _notificationService.GetUserNotificationsAsync(Guid.Parse(userId), cancellationToken);
        return Ok(notifications);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        // Ideally verify ownership
        await _notificationService.MarkAsReadAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        await _notificationService.MarkAllAsReadAsync(Guid.Parse(userId), cancellationToken);
        return NoContent();
    }
}
