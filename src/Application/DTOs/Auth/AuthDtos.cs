using Domain.Enums;

namespace Application.DTOs.Auth;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Player;
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public int? Age { get; set; }
    public Guid? GovernorateId { get; set; }
    public Guid? CityId { get; set; }
    public Guid? AreaId { get; set; }
    public string? IdFrontUrl { get; set; }
    public string? IdBackUrl { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public object? User { get; set; } // Using object to avoid circular dep if UserDto is used
    // Or better, define UserDto here or reference it. DTOs are in Application, so ok.
    // I need UserDto.
}
