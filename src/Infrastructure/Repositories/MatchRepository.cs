using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class MatchRepository : GenericRepository<Match>, IMatchRepository
{
    // PERF-FIX B14: Compiled query — skips expression tree compilation on every call
    private static readonly Func<AppDbContext, IAsyncEnumerable<MatchOutcomeDto>> _getFinishedOutcomes =
        EF.CompileAsyncQuery((AppDbContext ctx) =>
            ctx.Matches
                .Where(m => m.Status == MatchStatus.Finished)
                .Select(m => new MatchOutcomeDto
                {
                    HomeTeamId = m.HomeTeamId,
                    AwayTeamId = m.AwayTeamId,
                    HomeScore = m.HomeScore,
                    AwayScore = m.AwayScore
                }));

    public MatchRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<MatchOutcomeDto>> GetFinishedMatchOutcomesAsync(CancellationToken ct = default)
    {
        var results = new List<MatchOutcomeDto>();
        await foreach (var item in _getFinishedOutcomes(_context).WithCancellation(ct))
        {
            results.Add(item);
        }
        return results;
    }
}
