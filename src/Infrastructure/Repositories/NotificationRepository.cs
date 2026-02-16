using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    // PERF-FIX B14: Compiled query — skips expression tree compilation on every call
    private static readonly Func<AppDbContext, Guid, int, int, IAsyncEnumerable<Notification>> _getByUserId =
        EF.CompileAsyncQuery((AppDbContext ctx, Guid userId, int skip, int take) =>
            ctx.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Skip(skip)
                .Take(take));

    public NotificationRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, int pageSize = 30, int page = 1, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var results = new List<Notification>();
        await foreach (var item in _getByUserId(_context, userId, skip, pageSize).WithCancellation(ct))
        {
            results.Add(item);
        }
        return results;
    }

    /// <summary>Efficient unread count — single indexed COUNT</summary>
    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync(ct);
    }

    /// <summary>DTO projection at DB level — no entity materialization, uses covering index</summary>
    public async Task<(List<NotificationDto> Items, int TotalCount)> GetPagedDtoAsync(
        Guid userId, int page, int pageSize,
        NotificationCategory? category = null, bool? isRead = null,
        CancellationToken ct = default)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (category.HasValue)
            query = query.Where(n => n.Category == category.Value);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString().ToLower(),
                Category = n.Category.ToString().ToLower(),
                Priority = n.Priority.ToString().ToLower(),
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                EntityId = n.EntityId,
                EntityType = n.EntityType,
                ActionUrl = n.ActionUrl
            })
            .ToListAsync(ct);

        return (items, totalCount);
    }

    /// <summary>PERF-FIX B3: Single SQL UPDATE — no entity loading</summary>
    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    /// <summary>Duplicate detection within a time window</summary>
    public async Task<bool> HasRecentDuplicateAsync(Guid userId, string title, TimeSpan window, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - window;
        return await _context.Notifications
            .AnyAsync(n => n.UserId == userId && n.Title == title && n.CreatedAt >= cutoff, ct);
    }

    /// <summary>Bulk delete expired notifications — single SQL DELETE</summary>
    public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(n => n.ExpiresAt != null && n.ExpiresAt <= DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);
    }
}
