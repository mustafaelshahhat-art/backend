using System;
using System.Threading.Tasks;
using Application.DTOs.Auth;
using Application.Interfaces;
using Application.Common;
using Domain.Entities;
using Domain.Interfaces;
using Shared.Exceptions;
using Domain.Enums;
using AutoMapper;
using System.Linq;
using Application.DTOs.Users;
using System.Collections.Generic;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;
    private readonly INotificationService _notificationService;
    private readonly IRealTimeNotifier _notifier;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;

    public AuthService(
        IRepository<User> userRepository, 
        IRepository<Team> teamRepository, 
        IJwtTokenGenerator jwtTokenGenerator, 
        IPasswordHasher passwordHasher, 
        IMapper mapper, 
        IAnalyticsService analyticsService,
        INotificationService notificationService,
        IRealTimeNotifier notifier,
        IOtpService otpService,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
        _notifier = notifier;
        _otpService = otpService;
        _emailService = emailService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("Email and Name are required.");
        }

        var email = request.Email.Trim().ToLower();
        var name = request.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("Name is required.");
        }

        var existingUser = await _userRepository.FindAsync(u => u.Email.ToLower() == email);
        if (existingUser != null && existingUser.Any())
        {
            throw new ConflictException("Email already exists.");
        }

        var user = new User
        {
            Email = email,
            Name = name,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = (request.Role == UserRole.Admin) ? UserRole.Player : request.Role,
            Status = UserStatus.Pending, 
            DisplayId = "U-" + new Random().Next(1000, 9999),
            Phone = request.Phone?.Trim(),
            NationalId = request.NationalId?.Trim(),
            Age = request.Age,
            Governorate = request.Governorate,
            City = request.City,
            Neighborhood = request.Neighborhood,
            IdFrontUrl = request.IdFrontUrl,
            IdBackUrl = request.IdBackUrl
        };

        if (user.Role == UserRole.Player && string.IsNullOrEmpty(user.DisplayId))
        {
             // Logic for specific display ID can be better
        }

        await _userRepository.AddAsync(user);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.USER_REGISTERED, 
            new Dictionary<string, string> { { "userName", user.Name } }, 
            user.Id, 
            user.Name
        );

        var mappedUser = await MapUserWithTeamInfoAsync(user);

        // DELAYED: Notification moved to VerifyEmailAsync after actual confirmation

        try 
        {
            var otp = await _otpService.GenerateOtpAsync(user.Id, "EMAIL_VERIFY");
            var body = EmailTemplateHelper.CreateOtpTemplate(
                "تفعيل حسابك الجديد", 
                user.Name, 
                "شكراً لانضمامك إلينا! يرجى استخدام الرمز التالي لتفعيل حسابك والبدء في استخدام المنصة.", 
                otp, 
                "10 دقائق"
            );
            await _emailService.SendEmailAsync(user.Email, "تأكيد بريدك الإلكتروني – RAMADAN GANA", body);
        }
        catch (Exception ex)
        {
            // SURGICAL FIX: Throwing here ensures the user sees that email failed.
            // But User is created. Frontend can guide them to "Resend".
            // We modify the message to be friendly.
            throw new Exception("تم إنشاء الحساب ولكن فشل إرسال رمز التحقق. يرجى استخدام خيار 'إعادة الإرسال' في صفحة التحقق.");
        }

        // Persistent Notification for Admins
        await _notificationService.SendNotificationByTemplateAsync(Guid.Empty, NotificationTemplates.ADMIN_NEW_USER_REGISTERED, new Dictionary<string, string> 
        { 
            { "name", user.Name },
            { "role", user.Role.ToString() }
        });

        return new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            User = mappedUser
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("Email is required.");
        }

        var email = request.Email.Trim().ToLower();
        var users = await _userRepository.FindAsync(u => u.Email.ToLower() == email);
        var user = users.FirstOrDefault();

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new BadRequestException("Invalid email or password.");
        }

        // 1. SURGICAL FIX: Enforce Email Verification (Skip for Admins)
        if (!user.IsEmailVerified && user.Role != UserRole.Admin)
        {
            // NEW LOGIC: Generate a new OTP and resend email
            try 
            {
                var otp = await _otpService.GenerateOtpAsync(user.Id, "EMAIL_VERIFY");
                var body = EmailTemplateHelper.CreateOtpTemplate(
                    "تفعيل الحساب المطلوب", 
                    user.Name, 
                    "لقد حاولت تسجيل الدخول ولكن بريدك لم يتم تأكيده بعد. يرجى استخدام الرمز الجديد لتفعيل حسابك.", 
                    otp, 
                    "10 دقائق"
                );
                await _emailService.SendEmailAsync(user.Email, "تفعيل حسابك – RAMADAN GANA", body);
            }
            catch (Exception ex)
            {
                // Log but still throw the specific exception to trigger frontend redirect
                // However, user will see the message from EmailNotVerifiedException
            }

            throw new EmailNotVerifiedException(user.Email);
        }

        // 2. SURGICAL FIX: Enforce Active Status (Only block Suspended)
        if (user.Status == UserStatus.Suspended)
        {
             throw new ForbiddenException("Account is suspended.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user);
        
        try 
        {
            await _analyticsService.LogActivityByTemplateAsync(
                ActivityConstants.USER_LOGIN, 
                new Dictionary<string, string> { { "userName", user.Name } }, 
                user.Id, 
                user.Name
            );
        }
        catch 
        {
            // Don't fail login if analytics logging fails
        }

        return new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            User = await MapUserWithTeamInfoAsync(user)
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
    {
        var users = await _userRepository.FindAsync(u => u.RefreshToken == request.RefreshToken);
        var user = users.FirstOrDefault();

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new BadRequestException("Invalid or expired refresh token.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user);

        return new AuthResponse
        {
            Token = token,
            RefreshToken = newRefreshToken,
            User = await MapUserWithTeamInfoAsync(user)
        };
    }
    public async Task VerifyEmailAsync(string email, string otp)
    {
        var user = (await _userRepository.FindAsync(u => u.Email == email)).FirstOrDefault();
        if (user == null) throw new NotFoundException("User not found.");

        if (user.IsEmailVerified) return; // Already verified

        var isValid = await _otpService.VerifyOtpAsync(user.Id, otp, "EMAIL_VERIFY");
        if (!isValid) throw new BadRequestException("Invalid or expired OTP.");

        user.IsEmailVerified = true;
        
        await _userRepository.UpdateAsync(user);

        // NEW: Notify Admins that a new VERIFIED user joined
        var mappedUser = await MapUserWithTeamInfoAsync(user);
        await _notifier.SendUserCreatedAsync(mappedUser);
    }

    public async Task ForgotPasswordAsync(string email)
    {
        var user = (await _userRepository.FindAsync(u => u.Email == email)).FirstOrDefault();
        if (user == null) return; // Silent success

        var otp = await _otpService.GenerateOtpAsync(user.Id, "PASSWORD_RESET");

        // Send Email
        try
        {
            var body = EmailTemplateHelper.CreateOtpTemplate(
                "إعادة تعيين كلمة المرور", 
                user.Name, 
                "لقد تلقينا طلباً لإعادة تعيين كلمة المرور الخاصة بك. يرجى استخدام الرمز التالي للمتابعة.", 
                otp, 
                "10 دقائق"
            );
            await _emailService.SendEmailAsync(user.Email, "طلب إعادة تعيين كلمة المرور – RAMADAN GANA", body);
        }
        catch 
        {
            // Log
        }
    }

    public async Task ResetPasswordAsync(string email, string otp, string newPassword)
    {
        var user = (await _userRepository.FindAsync(u => u.Email == email)).FirstOrDefault();
        if (user == null) throw new NotFoundException("User not found.");

        var isValid = await _otpService.VerifyOtpAsync(user.Id, otp, "PASSWORD_RESET");
        if (!isValid) throw new BadRequestException("Invalid or expired OTP.");

        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        
        // Revoke tokens?
        user.RefreshToken = null; 
        
        await _userRepository.UpdateAsync(user);
    }

    public async Task ResendOtpAsync(string email, string type)
    {
        var user = (await _userRepository.FindAsync(u => u.Email == email)).FirstOrDefault();
        if (user == null) return; 

        if (type == "EMAIL_VERIFY" && user.IsEmailVerified) return;

        var otp = await _otpService.GenerateOtpAsync(user.Id, type);

        try
        {
            string title = type == "EMAIL_VERIFY" ? "تفعيل حسابك" : "إعادة تعيين كلمة المرور";
            string subject = type == "EMAIL_VERIFY" ? "تأكيد بريدك الإلكتروني" : "طلب إعادة تعيين كلمة المرور";
            string message = type == "EMAIL_VERIFY" 
                ? "لقد طلبت إعادة إرسال رمز التفعيل. يرجى استخدامه لتأكيد بريدك الإليكتروني." 
                : "لقد طلبت إعادة إرسال رمز استعادة الحساب. يرجى استخدامه لتعيين كلمة مرور جديدة.";

            var body = EmailTemplateHelper.CreateOtpTemplate(title, user.Name, message, otp, "10 دقائق");
            await _emailService.SendEmailAsync(user.Email, $"{subject} – RAMADAN GANA", body);
        }
        catch 
        {
            // If explicit user action to resend, throw so they know it failed
            throw new Exception("فشل إرسال البريد الإلكتروني. يرجى المحاولة لاحقاً.");
        }
    }

    private async Task<UserDto> MapUserWithTeamInfoAsync(User user)
    {
        var dto = _mapper.Map<UserDto>(user);
        if (user.TeamId.HasValue)
        {
            var team = await _teamRepository.GetByIdAsync(user.TeamId.Value);
            if (team != null)
            {
                dto.TeamName = team.Name;
                dto.IsTeamOwner = team.CaptainId == user.Id;
            }
        }
        return dto;
    }
}
