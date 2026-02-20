using Application.DTOs.Auth;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IRepository<User> _userRepository;
    private readonly IAuthUserResolverService _authUserResolver;
    private readonly IAuthTokenService _authToken;
    private readonly ISystemSettingsService _systemSettingsService;

    public LoginCommandHandler(
        IRepository<User> userRepository,
        IAuthUserResolverService authUserResolver,
        IAuthTokenService authToken,
        ISystemSettingsService systemSettingsService)
    {
        _userRepository = userRepository;
        _authUserResolver = authUserResolver;
        _authToken = authToken;
        _systemSettingsService = systemSettingsService;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var req = request.Request;
        if (req == null || string.IsNullOrWhiteSpace(req.Email))
            throw new BadRequestException("البريد الإلكتروني مطلوب. يرجى إدخال بريدك الإلكتروني لتسجيل الدخول.");

        var email = req.Email.Trim().ToLower();
        var users = await _userRepository.FindAsync(u => u.Email == email, ct);
        var user = users.FirstOrDefault();

        if (user == null || !_authToken.VerifyPassword(req.Password, user.PasswordHash))
            throw new BadRequestException("البريد الإلكتروني أو كلمة المرور غير صحيحة. يرجى التأكد من بيانات تسجيل الدخول والمحاولة مرة أخرى.");

        if (user.Role != UserRole.Admin && await _systemSettingsService.IsMaintenanceModeEnabledAsync(ct))
            throw new ForbiddenException("النظام تحت الصيانة حالياً. الدخول متاح لمديري النظام فقط.");

        if (!user.IsEmailVerified && user.Role != UserRole.Admin)
            throw new EmailNotVerifiedException(user.Email);

        if (user.Status == UserStatus.Suspended)
            throw new ForbiddenException("تم إيقاف حسابك. يرجى التواصل مع الإدارة.");

        var token = _authToken.GenerateToken(user);
        var refreshToken = _authToken.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, ct);

        return new AuthResponse
        {
            Token = token, RefreshToken = refreshToken,
            User = await _authUserResolver.ResolveUserWithTeamAsync(user, ct)
        };
    }
}
