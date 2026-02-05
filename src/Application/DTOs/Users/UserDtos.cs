using System;
using Domain.Enums;

namespace Application.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public string DisplayId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // returning string representation
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
    public DateTime CreatedAt { get; set; }
}

public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Avatar { get; set; }
    public string? City { get; set; }
    public string? Governorate { get; set; }
    public string? Neighborhood { get; set; }
    public int? Age { get; set; }
}
