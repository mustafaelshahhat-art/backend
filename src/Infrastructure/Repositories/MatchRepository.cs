using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class MatchRepository : GenericRepository<Match>, IMatchRepository
{
    public MatchRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<MatchOutcomeDto>> GetFinishedMatchOutcomesAsync()
    {
        return await _context.Matches
            .Where(m => m.Status == Domain.Enums.MatchStatus.Finished)
            .Select(m => new MatchOutcomeDto
            {
                HomeTeamId = m.HomeTeamId,
                AwayTeamId = m.AwayTeamId,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore
            })
            .ToListAsync();
    }
}
