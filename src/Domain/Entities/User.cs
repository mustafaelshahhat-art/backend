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
    public string? Phone { get; set; }
    public int? Age { get; set; }
    public string? NationalId { get; set; }

    // Location (normalized FK references)
    public Guid? GovernorateId { get; set; }
    public Guid? CityId { get; set; }
    public Guid? AreaId { get; set; }

    // Deprecated text fields — kept for migration, will be removed
    [Obsolete("Use GovernorateId instead. Kept for data migration.")]
    public string? Governorate { get; set; }
    [Obsolete("Use CityId instead. Kept for data migration.")]
    public string? City { get; set; }
    [Obsolete("Use AreaId instead. Kept for data migration.")]
    public string? Neighborhood { get; set; }

    public string? IdFrontUrl { get; set; }
    public string? IdBackUrl { get; set; }
    
    // Auth
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public int TokenVersion { get; set; } = 1;

    // Navigation properties
    public Governorate? GovernorateNav { get; set; }
    public City? CityNav { get; set; }
    public Area? AreaNav { get; set; }

    // If user is a captain context
    public Guid? TeamId { get; set; }
}
