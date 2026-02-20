namespace Application.Contracts.Notifications.Responses;

/// <summary>
/// Notification list item DTO.
/// Clean: no domain entity references, no FromEntity method.
/// </summary>
public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "info";
    public string Category { get; set; } = "system";
    public string Priority { get; set; } = "normal";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string? ActionUrl { get; set; }
}

/// <summary>
/// Response for unread notification count.
/// Replaces anonymous { count } object.
/// </summary>
public class UnreadCountResponse
{
    public int Count { get; set; }

    public UnreadCountResponse() { }
    public UnreadCountResponse(int count) => Count = count;
}
