using Domain.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Application.DTOs.Notifications;

namespace Application.Interfaces;

public interface INotificationRepository : IRepository<Notification>
{
    /// <summary>Efficient unread count — single SQL COUNT</summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Paginated DTO projection — no entity materialization</summary>
    Task<(List<NotificationDto> Items, int TotalCount)> GetPagedDtoAsync(
        Guid userId, int page, int pageSize,
        NotificationCategory? category = null, bool? isRead = null,
        CancellationToken ct = default);

    /// <summary>Bulk SQL UPDATE — no entity loading</summary>
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Check for recent duplicate (same user + title within window)</summary>
    Task<bool> HasRecentDuplicateAsync(Guid userId, string title, TimeSpan window, CancellationToken ct = default);

    /// <summary>Delete expired notifications in bulk</summary>
    Task<int> DeleteExpiredAsync(CancellationToken ct = default);

    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, int pageSize = 30, int page = 1, CancellationToken ct = default);
}
