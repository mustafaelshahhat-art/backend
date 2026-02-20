using Application.Contracts.Common;
using Application.DTOs.Auth;
using Application.Interfaces;
using MediatR;
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
    private readonly IMediator _mediator;
    private readonly IFileStorageService _fileStorage;

    public AuthController(IMediator mediator, IFileStorageService fileStorage)
    {
        _mediator = mediator;
        _fileStorage = fileStorage;
    }

    [HttpPost("register")]
    [Api.Infrastructure.Filters.FileValidation]
    public async Task<ActionResult<AuthResponse>> Register([FromForm] RegisterRequest request, IFormFile? idFront, IFormFile? idBack, CancellationToken cancellationToken)
    {
        if (idFront != null)
        {
            var frontName = $"{Guid.NewGuid()}{Path.GetExtension(idFront.FileName)}";
            request.IdFrontUrl = await _fileStorage.SaveFileAsync(idFront.OpenReadStream(), frontName, idFront.ContentType, cancellationToken);
        }
        if (idBack != null)
        {
            var backName = $"{Guid.NewGuid()}{Path.GetExtension(idBack.FileName)}";
            request.IdBackUrl = await _fileStorage.SaveFileAsync(idBack.OpenReadStream(), backName, idBack.ContentType, cancellationToken);
        }

        var command = new Application.Features.Auth.Commands.Register.RegisterCommand(request);
        var response = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Auth.Commands.Login.LoginCommand(request);
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("login-guest")]
    public async Task<ActionResult<MessageResponse>> LoginGuest(CancellationToken cancellationToken)
    {
        var command = new Application.Features.Auth.Commands.LogGuestVisit.LogGuestVisitCommand();
        await _mediator.Send(command, cancellationToken);
        return Ok(new MessageResponse("Guest visit logged."));
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Auth.Commands.RefreshToken.RefreshTokenCommand(request);
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<MessageResponse>> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Auth.Commands.VerifyEmail.VerifyEmailCommand(request.Email, request.Otp);
        await _mediator.Send(command, cancellationToken);
        return Ok(new MessageResponse("Email verified successfully."));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<MessageResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Auth.Commands.ForgotPassword.ForgotPasswordCommand(request.Email);
        await _mediator.Send(command, cancellationToken);
        return Ok(new MessageResponse("If the email exists, a reset code has been sent."));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<MessageResponse>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Auth.Commands.ResetPassword.ResetPasswordCommand(request.Email, request.Otp, request.NewPassword);
        await _mediator.Send(command, cancellationToken);
        return Ok(new MessageResponse("Password reset successfully."));
    }

    [HttpPost("resend-otp")]
    public async Task<ActionResult<MessageResponse>> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Auth.Commands.ResendOtp.ResendOtpCommand(request.Email, request.Type);
        await _mediator.Send(command, cancellationToken);
        return Ok(new MessageResponse("OTP resent successfully."));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdStr, out var userId))
        {
            var command = new Application.Features.Auth.Commands.Logout.LogoutCommand(userId);
            await _mediator.Send(command, cancellationToken);
        }
        return NoContent();
    }
}
