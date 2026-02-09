using System;

namespace Application.DTOs.Objections;

public class ObjectionDto
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid TeamId { get; set; }
    public Guid CaptainId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public string TournamentName { get; set; } = string.Empty;
    public string HomeTeamName { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmitObjectionRequest
{
    public Guid MatchId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ResolveObjectionRequest
{
    public bool Approved { get; set; }
    public string? Notes { get; set; }
}
