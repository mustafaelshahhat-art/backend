using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
// WAIT. Application Layer cannot depend on Api Layer (Clean Architecture).
// BAD ARCHITECTURE: Api.Hubs is in Api Layer.
// FIX: Hubs should remain in Api. Services in Application cannot reference Hubs directly if they are in Api.
// Pattern: Application defines IClientNotifier interface. Api implements it using HubContext.
// OR: Put Hub interface in Application?
// Simplest Fix for this task:
// 1. We need IHubContext<NotificationHub> to send messages.
// 2. Application layer should not reference SignalR or Api.Hubs.
// Solution:
// Define INotificationClient interface in Application/Interfaces? No, SignalR is specific.
// Standard clean arch:
// Application has INotifierService.
// Infrastructure implementation uses SignalR? Or Api implementation?
// Usually Infrastructure or a dedicated "Presentation" layer logic.
// But Hubs are typically ASP.NET Core specific.
// I will create 'NotificationService' in Application, but it needs to call something to push real-time.
// I will define 'IRealTimeNotifier' interface in Application.Interfaces.
// And implement it in Api or Infrastructure.
// Ideally, NotificationService just saves to DB, then calls IRealTimeNotifier.
// Let's create IRealTimeNotifier in Application.
// Then implement RealTimeNotifier in Api/Services/RealTimeNotifier.cs and inject it.

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

    public async Task SendNotificationAsync(Guid userId, string title, string message, string type = "system")
    {
        // Broadcast to all active admins (used by admin_broadcast notifications).
        if (userId == Guid.Empty)
        {
            var admins = await _userRepository.FindAsync(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active);
            foreach (var admin in admins)
            {
                var adminNotification = CreateNotification(admin.Id, title, message, type);
                await _repository.AddAsync(adminNotification);
                await _notifier.SafeSendNotificationAsync(admin.Id, adminNotification);
                
                // Lightweight System Event
                await _notifier.SendSystemEventAsync("NOTIFICATION_CREATED", new { UserId = admin.Id, NotificationId = adminNotification.Id }, $"user:{admin.Id}");
            }
            return;
        }

        var notification = CreateNotification(userId, title, message, type);
        await _repository.AddAsync(notification);
        await _notifier.SafeSendNotificationAsync(userId, notification);
        
        // Lightweight System Event
        await _notifier.SendSystemEventAsync("NOTIFICATION_CREATED", new { UserId = userId, NotificationId = notification.Id }, $"user:{userId}");
    }

    public async Task<IEnumerable<Notification>> GetUserNotificationsAsync(Guid userId)
    {
        return await _repository.GetByUserIdAsync(userId);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var notification = await _repository.GetByIdAsync(id);
        if (notification != null)
        {
            notification.IsRead = true;
            await _repository.UpdateAsync(notification);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await _repository.MarkAllAsReadAsync(userId);
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
