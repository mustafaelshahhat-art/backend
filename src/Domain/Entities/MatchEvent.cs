using System;
using Domain.Enums;

namespace Domain.Entities;

public class MatchEvent : BaseEntity
{
    public Guid MatchId { get; set; }
    public Match? Match { get; set; }

    public MatchEventType Type { get; set; }
    
    public Guid TeamId { get; set; } // The team this event belongs to
    
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    
    public int Minute { get; set; }
    public string? Description { get; set; }
}
