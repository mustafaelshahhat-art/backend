using System;
using Domain.Enums;

namespace Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.Info;
    public NotificationCategory Category { get; set; } = NotificationCategory.System;
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    public bool IsRead { get; set; }

    /// <summary>Related entity ID (match, tournament, team, user, etc.)</summary>
    public Guid? EntityId { get; set; }
    /// <summary>Related entity type name (e.g. "match", "tournament", "team")</summary>
    public string? EntityType { get; set; }
    /// <summary>Deep-link URL for the notification action</summary>
    public string? ActionUrl { get; set; }
    /// <summary>Auto-expire old notifications</summary>
    public DateTime? ExpiresAt { get; set; }
}
