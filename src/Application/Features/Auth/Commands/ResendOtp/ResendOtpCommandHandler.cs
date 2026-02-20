using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Auth.Commands.ResendOtp;

public class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, Unit>
{
    private readonly IRepository<User> _userRepository;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public ResendOtpCommandHandler(IRepository<User> userRepository, IOtpService otpService, IEmailService emailService)
    {
        _userRepository = userRepository;
        _otpService = otpService;
        _emailService = emailService;
    }

    public async Task<Unit> Handle(ResendOtpCommand request, CancellationToken ct)
    {
        var user = (await _userRepository.FindAsync(u => u.Email == request.Email, ct)).FirstOrDefault();
        if (user == null) return Unit.Value;

        if (request.Type == "EMAIL_VERIFY" && user.IsEmailVerified) return Unit.Value;

        var otp = await _otpService.GenerateOtpAsync(user.Id, request.Type, ct);

        try
        {
            string title = request.Type == "EMAIL_VERIFY" ? "تفعيل حسابك" : "إعادة تعيين كلمة المرور";
            string subject = request.Type == "EMAIL_VERIFY" ? "تأكيد بريدك الإلكتروني" : "طلب إعادة تعيين كلمة المرور";
            string message = request.Type == "EMAIL_VERIFY"
                ? "لقد طلبت إعادة إرسال رمز التفعيل. يرجى استخدامه لتأكيد بريدك الإليكتروني."
                : "لقد طلبت إعادة إرسال رمز استعادة الحساب. يرجى استخدامه لتعيين كلمة مرور جديدة.";

            var body = EmailTemplateHelper.CreateOtpTemplate(title, user.Name, message, otp, "10 دقائق");
            await _emailService.SendEmailAsync(user.Email, $"{subject} – Kora Zone 365", body, ct);
        }
        catch
        {
            throw new Exception("فشل إرسال البريد الإلكتروني. يرجى المحاولة لاحقاً.");
        }

        return Unit.Value;
    }
}
