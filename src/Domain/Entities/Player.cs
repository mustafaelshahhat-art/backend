using System;

namespace Domain.Entities;

public class Player : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayId { get; set; } = string.Empty; // P-1234
    public int Number { get; set; }
    public string Position { get; set; } = "Player";
    public string Status { get; set; } = "active";
    
    // Stats
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }

    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public Guid? UserId { get; set; } // If linked to a registered user
    public User? User { get; set; }
}
