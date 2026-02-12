using System;

namespace Domain.Entities;

public class Activity : BaseEntity
{
    public string Type { get; set; } = string.Empty; // match|system|user
    public string Message { get; set; } = string.Empty;
    public Guid? UserId { get; set; } // Optional: who triggered it
    public User? User { get; set; }
    public string? UserName { get; set; }
}
