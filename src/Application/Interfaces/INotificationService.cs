using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(Guid userId, string title, string message, string type = "system");
    Task SendNotificationByTemplateAsync(Guid userId, string templateKey, Dictionary<string, string> placeholders = null, string type = "system");
    Task<IEnumerable<Notification>> GetUserNotificationsAsync(Guid userId);
    Task MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync(Guid userId);
}
