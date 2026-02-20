using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
{
    private readonly IRepository<User> _userRepository;
    private readonly IOtpService _otpService;
    private readonly IEmailQueueService _emailQueue;

    public ForgotPasswordCommandHandler(IRepository<User> userRepository, IOtpService otpService, IEmailQueueService emailQueue)
    {
        _userRepository = userRepository;
        _otpService = otpService;
        _emailQueue = emailQueue;
    }

    public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken ct)
    {
        var user = (await _userRepository.FindAsync(u => u.Email == request.Email, ct)).FirstOrDefault();
        if (user == null) throw new NotFoundException("عذراً، هذا البريد الإلكتروني غير مسجل لدينا.");

        if (user.Role == UserRole.Admin)
            throw new ForbiddenException("لا يمكن استعادة كلمة المرور لحسابات الإدارة من هنا. يرجى التواصل مع الدعم الفني.");

        var otp = await _otpService.GenerateOtpAsync(user.Id, "PASSWORD_RESET", ct);
        var body = EmailTemplateHelper.CreateOtpTemplate(
            "إعادة تعيين كلمة المرور", user.Name,
            "لقد تلقينا طلباً لإعادة تعيين كلمة المرور الخاصة بك. يرجى استخدام الرمز التالي للمتابعة.",
            otp, "10 دقائق");
        await _emailQueue.EnqueueAsync(user.Email, "طلب إعادة تعيين كلمة المرور – Kora Zone 365", body, ct);

        return Unit.Value;
    }
}
