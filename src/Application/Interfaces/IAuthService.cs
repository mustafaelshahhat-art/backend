using System;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Auth;
using Domain.Entities;

namespace Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    
    // OTP & Password Management
    Task VerifyEmailAsync(string email, string otp, CancellationToken ct = default);
    Task ForgotPasswordAsync(string email, CancellationToken ct = default);
    Task ResetPasswordAsync(string email, string otp, string newPassword, CancellationToken ct = default);
    Task ResendOtpAsync(string email, string type, CancellationToken ct = default);
    Task LogoutAsync(Guid userId, CancellationToken ct = default);
    Task LogGuestVisitAsync(CancellationToken ct = default);
}
