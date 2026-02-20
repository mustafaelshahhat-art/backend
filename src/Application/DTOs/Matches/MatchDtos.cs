using System;
using System.Collections.Generic;

namespace Application.DTOs.Matches;

public class MatchDto
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Guid HomeTeamId { get; set; }
    public string HomeTeamName { get; set; } = string.Empty;
    public Guid AwayTeamId { get; set; }
    public string AwayTeamName { get; set; } = string.Empty;
    public int HomeScore { get; set; }
    public int AwayScore { get; set; }
    public int? GroupId { get; set; }
    public int? RoundNumber { get; set; }
    public string? StageName { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public string? TournamentName { get; set; }
    public Guid? TournamentCreatorId { get; set; }

    public List<MatchEventDto> Events { get; set; } = new();
}

public class MatchEventDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid TeamId { get; set; }
    public Guid? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public int Minute { get; set; }
}

public class AddMatchEventRequest
{
    public string Type { get; set; } = string.Empty;
    public Guid TeamId { get; set; }
    public Guid? PlayerId { get; set; }
    public int Minute { get; set; }
}

public class UpdateMatchRequest
{
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public string? Status { get; set; }
    public DateTime? Date { get; set; }

}


