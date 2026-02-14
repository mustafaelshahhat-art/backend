using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface INotificationRepository
{
    Task<Notification> AddAsync(Notification notification, CancellationToken ct = default);
    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
    Task DeleteAsync(Notification notification, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
