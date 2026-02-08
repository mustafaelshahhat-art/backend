namespace Application.DTOs.Auth;

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
    public string Type { get; set; } = string.Empty; // EMAIL_VERIFY | PASSWORD_RESET
}
