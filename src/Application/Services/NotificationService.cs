using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

namespace Application.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly IRealTimeNotifier _notifier;
    private readonly IRepository<User> _userRepository;

    public NotificationService(
        INotificationRepository repository,
        IRealTimeNotifier notifier,
        IRepository<User> userRepository)
    {
        _repository = repository;
        _notifier = notifier;
        _userRepository = userRepository;
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message, string type = "system", CancellationToken ct = default)
    {
        // Broadcast to all active admins (used by admin_broadcast notifications).
        if (userId == Guid.Empty)
        {
            var admins = await _userRepository.FindAsync(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active, ct);
            foreach (var admin in admins)
            {
                var adminNotification = CreateNotification(admin.Id, title, message, type);
                await _repository.AddAsync(adminNotification, ct);
                await _notifier.SafeSendNotificationAsync(admin.Id, adminNotification, ct);
                
                // Lightweight System Event
                await _notifier.SendSystemEventAsync("NOTIFICATION_CREATED", new { UserId = admin.Id, NotificationId = adminNotification.Id }, $"user:{admin.Id}", ct);
            }
            return;
        }

        var notification = CreateNotification(userId, title, message, type);
        await _repository.AddAsync(notification, ct);
        await _notifier.SafeSendNotificationAsync(userId, notification, ct);
        
        // Lightweight System Event
        await _notifier.SendSystemEventAsync("NOTIFICATION_CREATED", new { UserId = userId, NotificationId = notification.Id }, $"user:{userId}", ct);
    }

    public async Task SendNotificationByTemplateAsync(Guid userId, string templateKey, Dictionary<string, string>? placeholders = null, string type = "system", CancellationToken ct = default)
    {
        var (title, message) = NotificationTemplates.GetTemplate(templateKey, placeholders);
        await SendNotificationAsync(userId, title, message, type, ct);
    }

    public async Task<Application.Common.Models.PagedResult<Notification>> GetUserNotificationsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _repository.GetPagedAsync(
            page,
            pageSize,
            n => n.UserId == userId,
            q => q.OrderByDescending(n => n.CreatedAt),
            ct
        );

        return new Application.Common.Models.PagedResult<Notification>(items.ToList(), totalCount, page, pageSize);
    }

    public async Task MarkAsReadAsync(Guid id, CancellationToken ct = default)
    {
        var notification = await _repository.GetByIdAsync(id, ct);
        if (notification != null)
        {
            notification.IsRead = true;
            await _repository.UpdateAsync(notification, ct);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _repository.MarkAllAsReadAsync(userId, ct);
    }

    private static Notification CreateNotification(Guid userId, string title, string message, string type)
    {
        return new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
