using System;
using Domain.Enums;

namespace Domain.Entities;

public class Objection : BaseEntity
{
    public Guid MatchId { get; set; }
    public Match? Match { get; set; }

    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public ObjectionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    
    public ObjectionStatus Status { get; set; } = ObjectionStatus.Pending;
    public string? AdminNotes { get; set; }
}
