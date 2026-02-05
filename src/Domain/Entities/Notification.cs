using System;

namespace Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "system"; // match|system
    public bool IsRead { get; set; }
}
