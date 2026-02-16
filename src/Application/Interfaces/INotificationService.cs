using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Notifications;
using Domain.Enums;

namespace Application.Interfaces;

public interface INotificationService
{
    /// <summary>Send a notification with explicit metadata</summary>
    Task<NotificationDto> SendNotificationAsync(
        Guid userId, string title, string message,
        NotificationCategory category = NotificationCategory.System,
        NotificationType type = NotificationType.Info,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? entityId = null, string? entityType = null, string? actionUrl = null,
        CancellationToken ct = default);

    /// <summary>Send a notification using a template key (category/type/priority from template metadata)</summary>
    Task<NotificationDto> SendNotificationByTemplateAsync(
        Guid userId, string templateKey,
        Dictionary<string, string>? placeholders = null,
        Guid? entityId = null, string? entityType = null, string? actionUrl = null,
        CancellationToken ct = default);

    /// <summary>Send a notification to ALL users of a specific role (batch insert + single SignalR group call)</summary>
    Task<NotificationDto> SendNotificationToRoleAsync(
        UserRole role, string title, string message,
        NotificationCategory category = NotificationCategory.System,
        NotificationType type = NotificationType.Info,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? entityId = null, string? entityType = null, string? actionUrl = null,
        CancellationToken ct = default);

    Task<Application.Common.Models.PagedResult<NotificationDto>> GetUserNotificationsAsync(
        Guid userId, int page, int pageSize,
        NotificationCategory? category = null, bool? isRead = null,
        CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
    Task DeleteAsync(Guid notificationId, Guid userId, CancellationToken ct = default);

    /// <summary>Remove expired notifications (background job)</summary>
    Task CleanupExpiredAsync(CancellationToken ct = default);
}
