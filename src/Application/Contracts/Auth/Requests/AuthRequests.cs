namespace Application.Contracts.Auth.Requests;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "Player";
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

public class VerifyEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResendOtpRequest
{
    public string Email { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
