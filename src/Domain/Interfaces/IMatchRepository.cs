using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entities;

namespace Domain.Interfaces;

public interface IMatchRepository : IRepository<Match>
{
    // Returns lightweight projection of match outcomes for stats calculation
    Task<IEnumerable<MatchOutcomeDto>> GetFinishedMatchOutcomesAsync();
}

public class MatchOutcomeDto
{
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
}
