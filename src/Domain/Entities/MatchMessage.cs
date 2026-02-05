using System;

namespace Domain.Entities;

public class MatchMessage : BaseEntity
{
    public Guid MatchId { get; set; }
    public Match? Match { get; set; }

    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // Player, Captain, Referee, Admin
    
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
