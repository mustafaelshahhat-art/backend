using Application.DTOs.Auth;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [Api.Infrastructure.Filters.FileValidation]
    public async Task<ActionResult<AuthResponse>> Register([FromForm] RegisterRequest request, IFormFile? idFront, IFormFile? idBack, CancellationToken cancellationToken)
    {
        if (idFront != null) request.IdFrontUrl = await SaveFile(idFront, cancellationToken);
        if (idBack != null) request.IdBackUrl = await SaveFile(idBack, cancellationToken);

        var response = await _authService.RegisterAsync(request, cancellationToken);
        return Ok(response);
    }

    private async Task<string> SaveFile(IFormFile file, CancellationToken cancellationToken)
    {
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/uploads/{fileName}";
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("login-guest")]
    public async Task<IActionResult> LoginGuest(CancellationToken cancellationToken)
    {
        await _authService.LogGuestVisitAsync(cancellationToken);
        return Ok(new { message = "Guest visit logged." });
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await _authService.VerifyEmailAsync(request.Email, request.Otp, cancellationToken);
        return Ok(new { message = "Email verified successfully." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request.Email, cancellationToken);
        return Ok(new { message = "If the email exists, a reset code has been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request.Email, request.Otp, request.NewPassword, cancellationToken);
        return Ok(new { message = "Password reset successfully." });
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResendOtpAsync(request.Email, request.Type, cancellationToken);
        return Ok(new { message = "OTP resent successfully." });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
        {
            await _authService.LogoutAsync(userId, cancellationToken);
        }
        return NoContent();
    }
}
