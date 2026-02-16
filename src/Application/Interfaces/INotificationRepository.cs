using Domain.Interfaces;
using Domain.Entities;

namespace Application.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, int pageSize = 30, int page = 1, CancellationToken ct = default);
}
