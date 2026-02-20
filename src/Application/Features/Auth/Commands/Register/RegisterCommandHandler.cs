using Application.Common;
using Application.DTOs.Auth;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    private readonly IRepository<User> _userRepository;
    private readonly IAuthUserResolverService _authUserResolver;
    private readonly IAuthTokenService _authToken;
    private readonly IOtpService _otpService;
    private readonly IEmailQueueService _emailQueue;

    public RegisterCommandHandler(
        IRepository<User> userRepository,
        IAuthUserResolverService authUserResolver,
        IAuthTokenService authToken,
        IOtpService otpService,
        IEmailQueueService emailQueue)
    {
        _userRepository = userRepository;
        _authUserResolver = authUserResolver;
        _authToken = authToken;
        _otpService = otpService;
        _emailQueue = emailQueue;
    }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken ct)
    {
        var req = request.Request;
        if (req == null || string.IsNullOrWhiteSpace(req.Email))
            throw new BadRequestException("البريد الإلكتروني والاسم مطلوبان. يرجى ملء جميع الحقول المطلوبة.");

        var email = req.Email.Trim().ToLower();
        var name = req.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new BadRequestException("الاسم مطلوب. يرجى إدخال اسمك الكامل.");

        var existingUser = await _userRepository.FindAsync(u => u.Email == email, true, ct);
        if (existingUser != null && existingUser.Any())
            throw new ConflictException("البريد الإلكتروني مستخدم بالفعل. يرجى استخدام بريد إلكتروني آخر أو تسجيل الدخول بحسابك الحالي.");

        var user = new User
        {
            Email = email, Name = name,
            PasswordHash = _authToken.HashPassword(req.Password),
            Role = UserRole.Player, Status = UserStatus.Pending,
            DisplayId = "U-" + new Random().Next(1000, 9999),
            Phone = req.Phone?.Trim(), NationalId = req.NationalId?.Trim(),
            Age = req.Age, GovernorateId = req.GovernorateId,
            CityId = req.CityId, AreaId = req.AreaId,
            IdFrontUrl = req.IdFrontUrl, IdBackUrl = req.IdBackUrl,
            IsEmailVerified = false
        };

        await _userRepository.AddAsync(user, ct);

        var token = _authToken.GenerateToken(user);
        var refreshToken = _authToken.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, ct);

        var mappedUser = await _authUserResolver.ResolveUserWithTeamAsync(user, ct);

        var otp = await _otpService.GenerateOtpAsync(user.Id, "EMAIL_VERIFY", ct);
        var emailBody = EmailTemplateHelper.CreateOtpTemplate(
            "تفعيل حسابك الجديد", user.Name,
            "شكراً لانضمامك إلينا! يرجى استخدام الرمز التالي لتفعيل حسابك والبدء في استخدام المنصة.",
            otp, "10 دقائق");
        await _emailQueue.EnqueueAsync(user.Email, "تأكيد بريدك الإلكتروني – Kora Zone 365", emailBody, ct);

        return new AuthResponse { Token = token, RefreshToken = refreshToken, User = mappedUser };
    }
}
