using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
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

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        // PERF-FIX B3: Single SQL UPDATE instead of loading all unread notifications into memory
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }
}
