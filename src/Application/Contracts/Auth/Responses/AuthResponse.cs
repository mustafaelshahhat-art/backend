namespace Application.Contracts.Auth.Responses;

/// <summary>
/// Auth response after login/register/refresh.
/// </summary>
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public AuthUserDto? User { get; set; }
}

/// <summary>
/// Typed user info within auth responses.
/// Replaces the untyped object? in the old AuthResponse.
/// </summary>
public class AuthUserDto
{
    public Guid Id { get; set; }
    public string DisplayId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int? Age { get; set; }
    public Guid? GovernorateId { get; set; }
    public string? GovernorateNameAr { get; set; }
    public Guid? CityId { get; set; }
    public string? CityNameAr { get; set; }
    public Guid? AreaId { get; set; }
    public string? AreaNameAr { get; set; }
    public string? IdFrontUrl { get; set; }
    public string? IdBackUrl { get; set; }
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string? TeamRole { get; set; }
    public List<Guid> JoinedTeamIds { get; set; } = new();
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
