using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Services;

public class OutboxAdminService : IOutboxAdminService
{
    private readonly AppDbContext _dbContext;

    public OutboxAdminService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IEnumerable<OutboxMessage> Messages, int TotalCount)> GetDeadLetterMessagesAsync(int page, int pageSize, CancellationToken ct)
    {
        var query = _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.DeadLetter);

        var totalCount = await query.CountAsync(ct);
        var messages = await query
            .OrderByDescending(m => m.OccurredOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (messages, totalCount);
    }

    public async Task<bool> RetryDeadLetterMessageAsync(Guid messageId, CancellationToken ct)
    {
        var message = await _dbContext.OutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (message == null || message.Status != OutboxMessageStatus.DeadLetter)
        {
            return false;
        }

        message.Status = OutboxMessageStatus.Pending;
        message.RetryCount = 0;
        message.Error = null;
        message.DeadLetterReason = null;
        message.ScheduledAt = DateTime.UtcNow;
        message.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> ClearDeadLetterMessagesAsync(CancellationToken ct)
    {
        var deadLetters = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.DeadLetter)
            .ToListAsync(ct);

        _dbContext.OutboxMessages.RemoveRange(deadLetters);
        await _dbContext.SaveChangesAsync(ct);
        
        return deadLetters.Count;
    }
}
