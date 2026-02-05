using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface INotificationRepository
{
    Task<Notification> AddAsync(Notification notification);
    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId);
    Task<Notification?> GetByIdAsync(Guid id);
    Task UpdateAsync(Notification notification);
    Task DeleteAsync(Notification notification);
    Task MarkAllAsReadAsync(Guid userId);
}
