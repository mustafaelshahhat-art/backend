using System;
using System.Threading.Tasks;
using Application.DTOs.Auth;
using Domain.Entities;

namespace Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    
    // OTP & Password Management
    Task VerifyEmailAsync(string email, string otp);
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(string email, string otp, string newPassword);
    Task ResendOtpAsync(string email, string type);
}
