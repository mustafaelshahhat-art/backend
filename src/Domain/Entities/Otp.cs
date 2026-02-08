using System;

namespace Domain.Entities;

public class Otp : BaseEntity
{
    public Guid UserId { get; set; }
    public string OtpHash { get; set; } = string.Empty; // BCrypt hash
    public string Type { get; set; } = string.Empty; // EMAIL_VERIFY | PASSWORD_RESET
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; } = false;
    public int Attempts { get; set; } = 0;

    // Navigation
    public User? User { get; set; }
}
