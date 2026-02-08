using System;
using System.Text.Json.Serialization;
using Domain.Enums;

namespace Application.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string DisplayId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Phone { get; set; }
    public int? Age { get; set; }
    public string? NationalId { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? IdFrontUrl { get; set; }
    public string? IdBackUrl { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public bool IsTeamOwner { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<UserActivityDto> Activities { get; set; } = new();
}

/// <summary>
/// Publicly accessible user information. No sensitive ID or contact info.
/// </summary>
public class UserPublicDto
{
    public Guid Id { get; set; }
    public string DisplayId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Governorate { get; set; }
    public string? City { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public bool IsTeamOwner { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class UserActivityDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
    [JsonPropertyName("removeAvatar")]
    public bool RemoveAvatar { get; set; } = false;
    public string? City { get; set; }
    public string? Governorate { get; set; }
    public string? Neighborhood { get; set; }
    public int? Age { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class UploadAvatarRequest
{
    public string Base64Image { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for creating a new admin user.
/// Role is always forced to Admin on the backend.
/// </summary>
public class CreateAdminRequest
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Active;
}

/// <summary>
/// Response DTO containing admin count for safety checks.
/// </summary>
public class AdminCountDto
{
    public int TotalAdmins { get; set; }
    public bool IsLastAdmin { get; set; }
}

