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

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(notification, ct);
        await _context.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Notifications.FindAsync(new object[] { id }, ct);
    }

    public async Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Notification notification, CancellationToken ct = default)
    {
        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        foreach (var n in notifications)
        {
            n.IsRead = true;
        }

        await _context.SaveChangesAsync(ct);
    }
}
