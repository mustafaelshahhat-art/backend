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

    public MatchMessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<MatchMessage> AddAsync(MatchMessage message, CancellationToken ct = default)
    {
        await _context.MatchMessages.AddAsync(message, ct);
        await _context.SaveChangesAsync(ct);
        return message;
    }

    public async Task<IEnumerable<MatchMessage>> GetByMatchIdAsync(Guid matchId, CancellationToken ct = default)
    {
        return await _context.MatchMessages
            .Where(m => m.MatchId == matchId)
            .OrderBy(m => m.Timestamp) // Oldest first for chat history
            .ToListAsync(ct);
    }
}
