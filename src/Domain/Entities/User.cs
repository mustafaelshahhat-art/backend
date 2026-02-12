using System;
using Domain.Enums;

namespace Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayId { get; set; } = string.Empty; // U-1234
    
    public UserRole Role { get; set; } = UserRole.Player;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public bool IsEmailVerified { get; set; } = false;
    
    // Profile fields
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
    public int? Age { get; set; }
    public string? NationalId { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? IdFrontUrl { get; set; }
    public string? IdBackUrl { get; set; }
    
    // Auth
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public int TokenVersion { get; set; } = 1;

    // Navigation properties
    // If user is a captain context
    public Guid? TeamId { get; set; }
}
