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

public class MatchMessageRepository : IMatchMessageRepository
{
    private readonly AppDbContext _context;

    // PERF-FIX B14: Compiled query — skips expression tree compilation on every call
    private static readonly Func<AppDbContext, Guid, int, int, IAsyncEnumerable<MatchMessage>> _getByMatchId =
        EF.CompileAsyncQuery((AppDbContext ctx, Guid matchId, int skip, int take) =>
            ctx.MatchMessages
                .AsNoTracking()
                .Where(m => m.MatchId == matchId)
                .OrderByDescending(m => m.Timestamp)
                .Skip(skip)
                .Take(take));

    public MatchMessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MatchMessage> AddAsync(MatchMessage message, CancellationToken ct = default)
    {
        await _context.MatchMessages.AddAsync(message, ct);
        return message;
    }

    public async Task<IEnumerable<MatchMessage>> GetByMatchIdAsync(Guid matchId, int pageSize = 50, int page = 1, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var results = new List<MatchMessage>();
        await foreach (var item in _getByMatchId(_context, matchId, skip, pageSize).WithCancellation(ct))
        {
            results.Add(item);
        }
        // Return in chronological order
        results.Reverse();
        return results;
    }
}
