using System;

namespace Application.DTOs.Notifications;

public sealed class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info";             // "info" | "success" | "warning" | "error"
    public string Category { get; set; } = "system";       // "system" | "account" | "payments" | "tournament" | "match" | "team" | "administrative" | "security"
    public string Priority { get; set; } = "normal";       // "low" | "normal" | "high" | "urgent"
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string? ActionUrl { get; set; }

    /// <summary>Projects entity to DTO without loading navigation properties</summary>
    public static NotificationDto FromEntity(Domain.Entities.Notification n) => new()
    {
        Id = n.Id,
        Title = n.Title,
        Message = n.Message,
        Type = n.Type.ToString().ToLowerInvariant(),
        Category = n.Category.ToString().ToLowerInvariant(),
        Priority = n.Priority.ToString().ToLowerInvariant(),
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt,
        EntityId = n.EntityId,
        EntityType = n.EntityType,
        ActionUrl = n.ActionUrl
    };
}
