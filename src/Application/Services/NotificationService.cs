using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly IRealTimeNotifier _notifier;
    private readonly IRepository<User> _userRepository;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>Dedup window — prevent identical notifications within this period</summary>
    private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(1);

    /// <summary>Default expiry for non-critical notifications (90 days)</summary>
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(90);

    public NotificationService(
        INotificationRepository repository,
        IRealTimeNotifier notifier,
        IRepository<User> userRepository,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _notifier = notifier;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<NotificationDto> SendNotificationAsync(
        Guid userId, string title, string message,
        NotificationCategory category = NotificationCategory.System,
        NotificationType type = NotificationType.Info,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? entityId = null, string? entityType = null, string? actionUrl = null,
        CancellationToken ct = default)
    {
        // ── Admin broadcast (userId == Guid.Empty) ──
        if (userId == Guid.Empty)
        {
            var admins = await _userRepository.FindAsync(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active, ct);
            var adminList = admins.ToList();
            if (adminList.Count == 0)
                return CreateDto(Guid.Empty, title, message, category, type, priority, entityId, entityType, actionUrl);

            var notifications = adminList.Select(a =>
                CreateEntity(a.Id, title, message, category, type, priority, entityId, entityType, actionUrl)).ToList();
            await _repository.AddRangeAsync(notifications, ct);

            // Single SignalR call to role group (replaces N individual calls)
            var dto = NotificationDto.FromEntity(notifications[0]);
            await _notifier.SendToRoleGroupAsync(UserRole.Admin.ToString(), dto, ct);
            return dto;
        }

        // ── Dedup check ──
        if (await _repository.HasRecentDuplicateAsync(userId, title, DedupWindow, ct))
        {
            _logger.LogDebug("Dedup: skipping duplicate notification '{Title}' for user {UserId}", title, userId);
            return CreateDto(userId, title, message, category, type, priority, entityId, entityType, actionUrl);
        }

        var notification = CreateEntity(userId, title, message, category, type, priority, entityId, entityType, actionUrl);
        await _repository.AddAsync(notification, ct);

        var notifDto = NotificationDto.FromEntity(notification);
        await _notifier.SafeSendNotificationAsync(userId, notifDto, ct);

        return notifDto;
    }

    public async Task<NotificationDto> SendNotificationByTemplateAsync(
        Guid userId, string templateKey,
        Dictionary<string, string>? placeholders = null,
        Guid? entityId = null, string? entityType = null, string? actionUrl = null,
        CancellationToken ct = default)
    {
        var (title, message, category, type, priority) = NotificationTemplates.GetTemplate(templateKey, placeholders);
        return await SendNotificationAsync(userId, title, message, category, type, priority, entityId, entityType, actionUrl, ct);
    }

    public async Task<NotificationDto> SendNotificationToRoleAsync(
        UserRole role, string title, string message,
        NotificationCategory category = NotificationCategory.System,
        NotificationType type = NotificationType.Info,
        NotificationPriority priority = NotificationPriority.Normal,
        Guid? entityId = null, string? entityType = null, string? actionUrl = null,
        CancellationToken ct = default)
    {
        var users = await _userRepository.FindAsync(u => u.Role == role && u.Status == UserStatus.Active, ct);
        var userList = users.ToList();
        if (userList.Count == 0)
            return CreateDto(Guid.Empty, title, message, category, type, priority, entityId, entityType, actionUrl);

        var notifications = userList.Select(u =>
            CreateEntity(u.Id, title, message, category, type, priority, entityId, entityType, actionUrl)).ToList();
        await _repository.AddRangeAsync(notifications, ct);

        // Single SignalR call to role group
        var dto = NotificationDto.FromEntity(notifications[0]);
        await _notifier.SendToRoleGroupAsync(role.ToString(), dto, ct);
        return dto;
    }

    public async Task<Application.Common.Models.PagedResult<NotificationDto>> GetUserNotificationsAsync(
        Guid userId, int page, int pageSize,
        NotificationCategory? category = null, bool? isRead = null,
        CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _repository.GetPagedDtoAsync(userId, page, pageSize, category, isRead, ct);
        return new Application.Common.Models.PagedResult<NotificationDto>(items, totalCount, page, pageSize);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await _repository.GetUnreadCountAsync(userId, ct);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _repository.GetByIdAsync(notificationId, ct);
        if (notification != null && notification.UserId == userId)
        {
            notification.IsRead = true;
            await _repository.UpdateAsync(notification, ct);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _repository.MarkAllAsReadAsync(userId, ct);
    }

    public async Task DeleteAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await _repository.GetByIdAsync(notificationId, ct);
        if (notification != null && notification.UserId == userId)
        {
            await _repository.DeleteAsync(notification, ct);
        }
    }

    public async Task CleanupExpiredAsync(CancellationToken ct = default)
    {
        var count = await _repository.DeleteExpiredAsync(ct);
        if (count > 0)
            _logger.LogInformation("Cleaned up {Count} expired notifications", count);
    }

    // ── Private helpers ──

    private static Notification CreateEntity(
        Guid userId, string title, string message,
        NotificationCategory category, NotificationType type, NotificationPriority priority,
        Guid? entityId, string? entityType, string? actionUrl)
    {
        return new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Category = category,
            Priority = priority,
            IsRead = false,
            EntityId = entityId,
            EntityType = entityType,
            ActionUrl = actionUrl,
            ExpiresAt = priority >= NotificationPriority.High ? null : DateTime.UtcNow.Add(DefaultExpiry),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static NotificationDto CreateDto(
        Guid userId, string title, string message,
        NotificationCategory category, NotificationType type, NotificationPriority priority,
        Guid? entityId, string? entityType, string? actionUrl)
    {
        return new NotificationDto
        {
            Id = Guid.NewGuid(),
            Title = title,
            Message = message,
            Type = type.ToString().ToLowerInvariant(),
            Category = category.ToString().ToLowerInvariant(),
            Priority = priority.ToString().ToLowerInvariant(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            EntityId = entityId,
            EntityType = entityType,
            ActionUrl = actionUrl
        };
    }
}
