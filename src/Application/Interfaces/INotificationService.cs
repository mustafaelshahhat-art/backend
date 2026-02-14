using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(Guid userId, string title, string message, string type = "system", CancellationToken ct = default);
    Task SendNotificationByTemplateAsync(Guid userId, string templateKey, Dictionary<string, string>? placeholders = null, string type = "system", CancellationToken ct = default);
    Task<Application.Common.Models.PagedResult<Notification>> GetUserNotificationsAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
