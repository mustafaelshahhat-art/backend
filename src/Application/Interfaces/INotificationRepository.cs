using Domain.Interfaces;
using Domain.Entities;

namespace Application.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
