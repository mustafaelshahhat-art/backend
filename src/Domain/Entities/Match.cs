using System;
using System.Collections.Generic;
using Domain.Enums;

namespace Domain.Entities;

public class Match : BaseEntity
{
    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public Guid HomeTeamId { get; set; }
    public Team? HomeTeam { get; set; }

    public Guid AwayTeamId { get; set; }
    public Team? AwayTeam { get; set; }

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    public int? GroupId { get; set; }
    public int? RoundNumber { get; set; }
    public string? StageName { get; set; }
    
    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;
    public DateTime? Date { get; set; }
    public bool Forfeit { get; set; } = false;

    /// <summary>
    /// Marks this match as the Opening Match of the tournament.
    /// Always Round 1, Match 1 of its group.
    /// </summary>
    public bool IsOpeningMatch { get; set; } = false;

    public ICollection<MatchEvent> Events { get; set; } = new List<MatchEvent>();
}
