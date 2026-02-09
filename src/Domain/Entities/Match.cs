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
    public int? RoundNumber { get; set; } // 1 = Group Stage MD1 / R16, 2 = QF, etc.
    public string? StageName { get; set; } // "Group Stage", "Quarter Final", "Semi Final", "Final"
    
    public MatchStatus Status { get; set; } = MatchStatus.Scheduled;
    public DateTime? Date { get; set; }
    public bool Forfeit { get; set; } = false;
    
    // Referee
    public Guid? RefereeId { get; set; }
    public User? Referee { get; set; }

    public string? RefereeNotes { get; set; }

    public ICollection<MatchEvent> Events { get; set; } = new List<MatchEvent>();
    public ICollection<Objection> Objections { get; set; } = new List<Objection>();
}
