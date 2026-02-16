using System;
using System.Threading;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<AuthService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRepository<Player> _playerRepository;
    private readonly IEmailQueueService _emailQueue;

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
        IEmailService emailService,
        ILogger<AuthService> logger,
        IServiceScopeFactory scopeFactory,
        IRepository<Player> playerRepository,
        IEmailQueueService emailQueue)
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
        _logger = logger;
        _scopeFactory = scopeFactory;
        _playerRepository = playerRepository;
        _emailQueue = emailQueue;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("البريد الإلكتروني والاسم مطلوبان. يرجى ملء جميع الحقول المطلوبة.");
        }

        var email = request.Email.Trim().ToLower();
        var name = request.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BadRequestException("الاسم مطلوب. يرجى إدخال اسمك الكامل.");
        }

        // PERF-FIX: Compare directly against normalized email — email is stored lowercase on write
        // This allows SQL Server to use the UQ_Users_Email index instead of full table scan
        var existingUser = await _userRepository.FindAsync(u => u.Email == email, true, ct);
        if (existingUser != null && existingUser.Any())
        {
            throw new ConflictException("البريد الإلكتروني مستخدم بالفعل. يرجى استخدام بريد إلكتروني آخر أو تسجيل الدخول بحسابك الحالي.");
        }

        // SECURITY: Force Player role for ALL public registrations.
        // Ignore any role sent in the request body to prevent privilege escalation.
        var user = new User
        {
            Email = email,
            Name = name,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Player, // FORCED - public registration always creates Player
            Status = UserStatus.Pending, 
            DisplayId = "U-" + new Random().Next(1000, 9999),
            Phone = request.Phone?.Trim(),
            NationalId = request.NationalId?.Trim(),
            Age = request.Age,
            GovernorateId = request.GovernorateId,
            CityId = request.CityId,
            AreaId = request.AreaId,
            IdFrontUrl = request.IdFrontUrl,
            IdBackUrl = request.IdBackUrl,
            IsEmailVerified = false
        };

        if (user.Role == UserRole.Player && string.IsNullOrEmpty(user.DisplayId))
        {
             // Logic for specific display ID can be better
        }

        await _userRepository.AddAsync(user, ct);

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, ct);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.USER_REGISTERED, 
            new Dictionary<string, string> { { "userName", user.Name } }, 
            user.Id, 
            user.Name,
            ct
        );

        var mappedUser = await MapUserWithTeamInfoAsync(user, ct);

        // DELAYED: Notification moved to VerifyEmailAsync after actual confirmation

        // OTP generation is fast (DB), keep it sync to ensure it exists
        var otp = await _otpService.GenerateOtpAsync(user.Id, "EMAIL_VERIFY", ct);
        
        // PERF-FIX B4: Enqueue to channel-based background service instead of Task.Run
        var emailBody = EmailTemplateHelper.CreateOtpTemplate(
            "تفعيل حسابك الجديد", 
            user.Name, 
            "شكراً لانضمامك إلينا! يرجى استخدام الرمز التالي لتفعيل حسابك والبدء في استخدام المنصة.", 
            otp, 
            "10 دقائق"
        );
        await _emailQueue.EnqueueAsync(user.Email, "تأكيد بريدك الإلكتروني – RAMADAN GANA", emailBody, ct);

        /* MOVED TO VERIFY EMAIL
        // Persistent Notification for Admins
        await _notificationService.SendNotificationByTemplateAsync(Guid.Empty, NotificationTemplates.ADMIN_NEW_USER_REGISTERED, new Dictionary<string, string> 
        { 
            { "name", user.Name },
            { "role", user.Role.ToString() }
        }, ct: ct);
        */

        return new AuthResponse
        {
            Token = token,
            RefreshToken = refreshToken,
            User = mappedUser
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("البريد الإلكتروني مطلوب. يرجى إدخال بريدك الإلكتروني لتسجيل الدخول.");
        }

        var email = request.Email.Trim().ToLower();
        // PERF-FIX: Compare directly — email is stored lowercase, avoids full table scan
        var users = await _userRepository.FindAsync(u => u.Email == email, ct);
        var user = users.FirstOrDefault();

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new BadRequestException("البريد الإلكتروني أو كلمة المرور غير صحيحة. يرجى التأكد من بيانات تسجيل الدخول والمحاولة مرة أخرى.");
        }

        // 1. SURGICAL FIX: Enforce Email Verification (Skip for Admins)
        // TEMPORARY DISABLE OTP
        if (!user.IsEmailVerified && user.Role != UserRole.Admin)
        {
            // NEW LOGIC: Generate a new OTP and resend email in background
            /* AUTO-RESEND DISABLED TO PREVENT RACE CONDITIONS
            try 
            {
                var otp = await _otpService.GenerateOtpAsync(user.Id, "EMAIL_VERIFY", ct);
                
                _ = Task.Run(async () => 
                {
                    using var scope = _scopeFactory.CreateScope();
                    var emailSvc = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    try {
                        var body = EmailTemplateHelper.CreateOtpTemplate(
                            "تفعيل الحساب المطلوب", 
                            user.Name, 
                            "لقد حاولت تسجيل الدخول ولكن بريدك لم يتم تأكيده بعد. يرجى استخدام الرمز الجديد لتفعيل حسابك.", 
                            otp, 
                            "10 دقائق"
                        );
                        await emailSvc.SendEmailAsync(user.Email, "تفعيل حسابك – RAMADAN GANA", body);
                    } catch {  }
                });
            }
            catch (Exception ex)
            {
                // OTP Gen failure or Task.Run failure, ignore to let LoginException proceed
            }
            */

            throw new EmailNotVerifiedException(user.Email);
        }

        // 2. Enforce Active Status: Block Suspended only. 
        // Pending users are allowed to login (Read-Only access handled by Policies/Frontend)
        if (user.Status == UserStatus.Suspended)
        {
            throw new ForbiddenException("تم إيقاف حسابك. يرجى التواصل مع الإدارة.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, ct);
        
        try 
        {
            await _analyticsService.LogActivityByTemplateAsync(
                ActivityConstants.USER_LOGIN, 
                new Dictionary<string, string> { { "userName", user.Name } }, 
                user.Id, 
                user.Name,
                ct
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
            User = await MapUserWithTeamInfoAsync(user, ct)
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        var users = await _userRepository.FindAsync(u => u.RefreshToken == request.RefreshToken, ct);
        var user = users.FirstOrDefault();

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            throw new BadRequestException("انتهت صلاحية الجلسة. يرجى تسجيل الدخول مرة أخرى.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateAsync(user, ct);

        return new AuthResponse
        {
            Token = token,
            RefreshToken = newRefreshToken,
            User = await MapUserWithTeamInfoAsync(user, ct)
        };
    }
    public async Task VerifyEmailAsync(string email, string otp, CancellationToken ct = default)
    {
        // CRITICAL FIX: Normalize email to lowercase before lookup (matches registration behavior)
        var normalizedEmail = email?.Trim().ToLower() ?? string.Empty;
        
        _logger.LogInformation($"[VerifyEmail] Attempting verification. Original: '{email}', Normalized: '{normalizedEmail}', OTP: '{otp}'");

        // PERF-FIX: Compare directly — email is stored lowercase, avoids full table scan
        var user = (await _userRepository.FindAsync(u => u.Email == normalizedEmail, ct)).FirstOrDefault();
        
        if (user == null) 
        {
            _logger.LogError($"[VerifyEmail] FAILURE: User not found for email: '{normalizedEmail}' (Original: '{email}')");
            throw new NotFoundException("لم يتم العثور على حساب مرتبط بهذا البريد الإلكتروني. يرجى التأكد من البريد المدخل.");
        }

        _logger.LogInformation($"[VerifyEmail] User found: {user.Id}, IsEmailVerified: {user.IsEmailVerified}");

        if (user.IsEmailVerified) return; // Already verified

        var isValid = await _otpService.VerifyOtpAsync(user.Id, otp, "EMAIL_VERIFY", ct);
        if (!isValid) 
        {
            _logger.LogWarning($"[VerifyEmail] INVALID OTP for User: {user.Id}. Provided: {otp}");
            throw new BadRequestException("كود التفعيل غير صحيح أو منتهي الصلاحية.");
        }
        
        _logger.LogInformation($"[VerifyEmail] OTP Valid. Updating status for User: {user.Id}");

        user.IsEmailVerified = true;
        // Status stays Pending - Admin must approve before user can login
        
        await _userRepository.UpdateAsync(user, ct);

        // Notify Admins: User verified their email, now needs admin review/approval
        var mappedUser = await MapUserWithTeamInfoAsync(user, ct);
        await _notifier.SendUserCreatedAsync(mappedUser, ct);
        
        // Persistent notification to admins for review
        await _notificationService.SendNotificationByTemplateAsync(
            Guid.Empty, 
            NotificationTemplates.ADMIN_USER_VERIFIED_PENDING_APPROVAL, 
            new Dictionary<string, string> 
            { 
                { "name", user.Name },
                { "email", user.Email },
                { "role", user.Role.ToString() }
            }, 
            ct: ct);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var user = (await _userRepository.FindAsync(u => u.Email == email, ct)).FirstOrDefault();
        if (user == null) throw new NotFoundException("عذراً، هذا البريد الإلكتروني غير مسجل لدينا.");

        if (user.Role == UserRole.Admin)
        {
            throw new ForbiddenException("لا يمكن استعادة كلمة المرور لحسابات الإدارة من هنا. يرجى التواصل مع الدعم الفني.");
        }

        var otp = await _otpService.GenerateOtpAsync(user.Id, "PASSWORD_RESET", ct);

        // PERF-FIX B4: Enqueue to channel-based background service instead of Task.Run
        var body = EmailTemplateHelper.CreateOtpTemplate(
            "إعادة تعيين كلمة المرور", 
            user.Name, 
            "لقد تلقينا طلباً لإعادة تعيين كلمة المرور الخاصة بك. يرجى استخدام الرمز التالي للمتابعة.", 
            otp, 
            "10 دقائق"
        );
        await _emailQueue.EnqueueAsync(user.Email, "طلب إعادة تعيين كلمة المرور – RAMADAN GANA", body, ct);
    }

    public async Task ResetPasswordAsync(string email, string otp, string newPassword, CancellationToken ct = default)
    {
        var normalizedEmail = email?.Trim().ToLower() ?? string.Empty;
        var user = (await _userRepository.FindAsync(u => u.Email == normalizedEmail, ct)).FirstOrDefault();
        if (user == null) throw new NotFoundException("لم يتم العثور على حساب مرتبط بهذا البريد الإلكتروني.");

        var isValid = await _otpService.VerifyOtpAsync(user.Id, otp, "PASSWORD_RESET", ct);
        if (!isValid) throw new BadRequestException("كود التفعيل غير صحيح أو منتهي الصلاحية.");

        // Actually reset the password
        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        // Invalidate all existing sessions
        user.RefreshToken = null;
        user.TokenVersion++;
        
        await _userRepository.UpdateAsync(user, ct);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user != null)
        {
            user.RefreshToken = null;
            user.TokenVersion++; // Invalidate all existing access tokens
            await _userRepository.UpdateAsync(user, ct);
        }
    }

    public async Task LogGuestVisitAsync(CancellationToken ct = default)
    {
        await _analyticsService.LogActivityAsync(
            ActivityConstants.GUEST_VISIT,
            "دخل زائر جديد إلى المنصة كضيف",
            null,
            "ضيف",
            ct
        );
    }

    public async Task ResendOtpAsync(string email, string type, CancellationToken ct = default)
    {
        var user = (await _userRepository.FindAsync(u => u.Email == email, ct)).FirstOrDefault();
        if (user == null) return; 

        if (type == "EMAIL_VERIFY" && user.IsEmailVerified) return;

        var otp = await _otpService.GenerateOtpAsync(user.Id, type, ct);

        try
        {
            string title = type == "EMAIL_VERIFY" ? "تفعيل حسابك" : "إعادة تعيين كلمة المرور";
            string subject = type == "EMAIL_VERIFY" ? "تأكيد بريدك الإلكتروني" : "طلب إعادة تعيين كلمة المرور";
            string message = type == "EMAIL_VERIFY" 
                ? "لقد طلبت إعادة إرسال رمز التفعيل. يرجى استخدامه لتأكيد بريدك الإليكتروني." 
                : "لقد طلبت إعادة إرسال رمز استعادة الحساب. يرجى استخدامه لتعيين كلمة مرور جديدة.";

            var body = EmailTemplateHelper.CreateOtpTemplate(title, user.Name, message, otp, "10 دقائق");
            await _emailService.SendEmailAsync(user.Email, $"{subject} – RAMADAN GANA", body, ct);
        }
        catch 
        {
            // If explicit user action to resend, throw so they know it failed
            throw new Exception("فشل إرسال البريد الإلكتروني. يرجى المحاولة لاحقاً.");
        }
    }

    private async Task<UserDto> MapUserWithTeamInfoAsync(User user, CancellationToken ct = default)
    {
        var dto = _mapper.Map<UserDto>(user);
        if (user.TeamId.HasValue)
        {
            var team = await _teamRepository.GetByIdAsync(user.TeamId.Value, ct);
            if (team != null)
            {
                dto.TeamName = team.Name;
                var player = (await _playerRepository.FindAsync(p => p.TeamId == user.TeamId.Value && p.UserId == user.Id, ct)).FirstOrDefault();
                dto.TeamRole = player?.TeamRole.ToString();
            }
        }
        return dto;
    }
}
