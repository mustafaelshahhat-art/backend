using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Application.Features.Auth.Commands.VerifyEmail;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, Unit>
{
    private readonly IRepository<User> _userRepository;
    private readonly IAuthUserResolverService _authUserResolver;
    private readonly IOtpService _otpService;
    private readonly ITeamNotificationFacade _teamNotifier;
    private readonly ILogger<VerifyEmailCommandHandler> _logger;

    public VerifyEmailCommandHandler(
        IRepository<User> userRepository,
        IAuthUserResolverService authUserResolver,
        IOtpService otpService,
        ITeamNotificationFacade teamNotifier,
        ILogger<VerifyEmailCommandHandler> logger)
    {
        _userRepository = userRepository;
        _authUserResolver = authUserResolver;
        _otpService = otpService;
        _teamNotifier = teamNotifier;
        _logger = logger;
    }

    public async Task<Unit> Handle(VerifyEmailCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email?.Trim().ToLower() ?? string.Empty;
        _logger.LogInformation("[VerifyEmail] Attempting verification. Original: '{Email}', Normalized: '{NormalizedEmail}', OTP: '{Otp}'", request.Email, normalizedEmail, request.Otp);

        var user = (await _userRepository.FindAsync(u => u.Email == normalizedEmail, ct)).FirstOrDefault();
        if (user == null)
        {
            _logger.LogError("[VerifyEmail] FAILURE: User not found for email: '{NormalizedEmail}'", normalizedEmail);
            throw new NotFoundException("لم يتم العثور على حساب مرتبط بهذا البريد الإلكتروني. يرجى التأكد من البريد المدخل.");
        }

        _logger.LogInformation("[VerifyEmail] User found: {UserId}, IsEmailVerified: {IsVerified}", user.Id, user.IsEmailVerified);
        if (user.IsEmailVerified) return Unit.Value;

        var isValid = await _otpService.VerifyOtpAsync(user.Id, request.Otp, "EMAIL_VERIFY", ct);
        if (!isValid)
        {
            _logger.LogWarning("[VerifyEmail] INVALID OTP for User: {UserId}. Provided: {Otp}", user.Id, request.Otp);
            throw new BadRequestException("كود التفعيل غير صحيح أو منتهي الصلاحية.");
        }

        _logger.LogInformation("[VerifyEmail] OTP Valid. Updating status for User: {UserId}", user.Id);
        user.IsEmailVerified = true;
        await _userRepository.UpdateAsync(user, ct);

        var mappedUser = await _authUserResolver.ResolveUserWithTeamAsync(user, ct);
        await _teamNotifier.SendUserCreatedAsync(mappedUser, ct);

        await _teamNotifier.NotifyByTemplateAsync(
            Guid.Empty, NotificationTemplates.ADMIN_USER_VERIFIED_PENDING_APPROVAL,
            new Dictionary<string, string>
            {
                { "name", user.Name }, { "email", user.Email }, { "role", user.Role.ToString() }
            }, ct: ct);

        return Unit.Value;
    }
}
