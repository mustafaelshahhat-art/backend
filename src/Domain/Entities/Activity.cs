using System;
using Domain.Enums;

namespace Domain.Entities;

public class Activity : BaseEntity
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string? UserName { get; set; }

    // ── Enhanced audit fields ──
    public ActivitySeverity Severity { get; set; } = ActivitySeverity.Info;
    public string? ActorRole { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? Metadata { get; set; }
}
