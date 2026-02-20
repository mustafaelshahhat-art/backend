using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly IRepository<User> _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpService _otpService;

    public ResetPasswordCommandHandler(IRepository<User> userRepository, IPasswordHasher passwordHasher, IOtpService otpService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _otpService = otpService;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email?.Trim().ToLower() ?? string.Empty;
        var user = (await _userRepository.FindAsync(u => u.Email == normalizedEmail, ct)).FirstOrDefault();
        if (user == null) throw new NotFoundException("لم يتم العثور على حساب مرتبط بهذا البريد الإلكتروني.");

        var isValid = await _otpService.VerifyOtpAsync(user.Id, request.Otp, "PASSWORD_RESET", ct);
        if (!isValid) throw new BadRequestException("كود التفعيل غير صحيح أو منتهي الصلاحية.");

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.RefreshToken = null;
        user.TokenVersion++;
        await _userRepository.UpdateAsync(user, ct);

        return Unit.Value;
    }
}
