using System;

namespace Application.DTOs.Teams;

public class PlayerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayId { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Position { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public Guid TeamId { get; set; }
}

public class AddPlayerRequest
{
    public string DisplayId { get; set; } = string.Empty;
}

public class JoinRequestDto
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestDate { get; set; }
    public bool InitiatedByPlayer { get; set; }
}

public class RespondJoinRequest
{
    public bool Approve { get; set; }
}
