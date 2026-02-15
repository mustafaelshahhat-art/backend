using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Users;
using Application.Interfaces;
using Application.Common;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;

namespace Application.Services;

public class UserService : IUserService
{
    private readonly IFileStorageService _fileStorage;
    private readonly IUserCacheService _userCache;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Activity> _activityRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly INotificationService _notificationService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAnalyticsService _analyticsService;
    private readonly ISystemSettingsService _systemSettingsService;

    public UserService(
        IRepository<User> userRepository, 
        IRepository<Activity> activityRepository,
        IRepository<Team> teamRepository,
        IMapper mapper,
        IRealTimeNotifier realTimeNotifier,
        INotificationService notificationService,
        IPasswordHasher passwordHasher,
        IAnalyticsService analyticsService,
        ISystemSettingsService systemSettingsService,
        IFileStorageService fileStorage,
        IUserCacheService userCache)
    {
        _userRepository = userRepository;
        _activityRepository = activityRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
        _notificationService = notificationService;
        _passwordHasher = passwordHasher;
        _analyticsService = analyticsService;
        _systemSettingsService = systemSettingsService;
        _fileStorage = fileStorage;
        _userCache = userCache;
    }

    public async Task<Application.Common.Models.PagedResult<UserDto>> GetPagedAsync(int pageNumber, int pageSize, string? role = null, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        Expression<Func<User, bool>>? predicate = null;
        if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, true, out var userRole))
        {
            predicate = u => u.Role == userRole && u.IsEmailVerified;
        }
        else
        {
            predicate = u => u.IsEmailVerified;
        }

        var result = await _userRepository.GetPagedAsync(pageNumber, pageSize, predicate, q => q.OrderBy(u => u.Name), ct);
        var dtos = _mapper.Map<List<UserDto>>(result.Items);
        return new Application.Common.Models.PagedResult<UserDto>(dtos, result.TotalCount, pageNumber, pageSize);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // PROD-AUDIT: Use consolidated query with AsNoTracking. Avoid multiple DB roundtrips.
        var user = await _userRepository.GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
            
        if (user == null) return null;

        var dto = _mapper.Map<UserDto>(user);

        // Fetch Team Details and Recent Activities in parallel or consolidated
        if (user.TeamId.HasValue)
        {
            var team = await _teamRepository.GetQueryable()
                .AsNoTracking()
                .Include(t => t.Players)
                .FirstOrDefaultAsync(t => t.Id == user.TeamId.Value);

            if (team != null)
            {
                var player = team.Players.FirstOrDefault(p => p.UserId == id);
                dto.TeamName = team.Name;
                dto.TeamRole = player?.TeamRole.ToString();
            }
        }
        else
        {
            var ownedTeam = (await _teamRepository.GetQueryable()
                .AsNoTracking()
                .Include(t => t.Players)
                .FirstOrDefaultAsync(t => t.Players.Any(p => p.UserId == id && p.TeamRole == TeamRole.Captain)));
            
            if (ownedTeam != null)
            {
                dto.TeamId = ownedTeam.Id;
                dto.TeamName = ownedTeam.Name;
                dto.TeamRole = TeamRole.Captain.ToString();
            }
        }

        var activities = await _activityRepository.GetQueryable()
            .AsNoTracking()
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToListAsync();
            
        dto.Activities = _mapper.Map<List<UserActivityDto>>(activities);

        return dto;
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null) throw new NotFoundException(nameof(User), id);

        if (!string.IsNullOrEmpty(request.Name)) user.Name = request.Name!;
        if (!string.IsNullOrEmpty(request.Phone)) user.Phone = request.Phone;
        
        // Handle avatar: RemoveAvatar takes precedence, then check for new avatar
        if (request.RemoveAvatar)
        {
            user.Avatar = null;
        }
        else if (!string.IsNullOrEmpty(request.Avatar))
        {
            user.Avatar = request.Avatar;
        }
        
        if (!string.IsNullOrEmpty(request.City)) user.City = request.City;
        if (!string.IsNullOrEmpty(request.Governorate)) user.Governorate = request.Governorate;
        if (!string.IsNullOrEmpty(request.Neighborhood)) user.Neighborhood = request.Neighborhood;
        if (request.Age.HasValue) user.Age = request.Age;

        await _userRepository.UpdateAsync(user, ct);
        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto, ct);
        return dto;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null) throw new NotFoundException(nameof(User), id);

        // CRITICAL SAFETY: Prevent deleting the last admin
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await GetAdminCountAsync(id);
            if (adminCount.IsLastAdmin)
            {
                throw new BadRequestException("لا يمكن حذف آخر مشرف في النظام");
            }
        }

        await _realTimeNotifier.SendAccountStatusChangedAsync(id, "Deleted");
        await _realTimeNotifier.SendUserDeletedAsync(id);
        await _userRepository.DeleteAsync(user, ct);
        await _userCache.InvalidateUserAsync(id, ct);
    }

    public async Task SuspendAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null) throw new NotFoundException(nameof(User), id);

        // CRITICAL SAFETY: Prevent suspending the last admin
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await GetAdminCountAsync(id);
            if (adminCount.IsLastAdmin)
            {
                throw new BadRequestException("لا يمكن إيقاف آخر مدير نشط في النظام.");
            }
        }

        user.Status = UserStatus.Suspended;
        user.TokenVersion++; // Invalidate all existing tokens
        await _userRepository.UpdateAsync(user, ct);
        await _userCache.InvalidateUserAsync(id, ct);
        await _realTimeNotifier.SendAccountStatusChangedAsync(id, UserStatus.Suspended.ToString());

        // Persistent Notification
        await _notificationService.SendNotificationByTemplateAsync(id, NotificationTemplates.ACCOUNT_SUSPENDED, new Dictionary<string, string>(), "all", ct);

        // Full Update for Lists
        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto, ct);

        // Lightweight System Event
        await _realTimeNotifier.SendSystemEventAsync("USER_BLOCKED", new { UserId = id }, $"user:{id}", ct);
    }

    public async Task ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null) throw new NotFoundException(nameof(User), id);

        user.Status = UserStatus.Active;
        await _userRepository.UpdateAsync(user, ct);
        await _realTimeNotifier.SendAccountStatusChangedAsync(id, UserStatus.Active.ToString());

        // Persistent Notification
        await _notificationService.SendNotificationByTemplateAsync(id, NotificationTemplates.ACCOUNT_APPROVED, new Dictionary<string, string>(), "all", ct);

        // Full Update for Lists
        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto, ct);

        // Lightweight System Event
        await _realTimeNotifier.SendSystemEventAsync("USER_APPROVED", new { UserId = id }, $"user:{id}", ct);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            throw new NotFoundException(nameof(User), userId);
        }

        // Verify current password
        if (!_passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            throw new BadRequestException("كلمة المرور الحالية غير صحيحة");
        }

        // Validate new password
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            throw new BadRequestException("كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل");
        }

        // Update password
        user.PasswordHash = _passwordHasher.HashPassword(newPassword);
        user.TokenVersion++; // Invalidate all existing tokens on password change
        await _userRepository.UpdateAsync(user, ct);

        // Send notification or real-time update if needed
        await _realTimeNotifier.SendAccountStatusChangedAsync(userId, "PasswordChanged");
        await _notificationService.SendNotificationByTemplateAsync(userId, NotificationTemplates.PASSWORD_CHANGED, new Dictionary<string, string>(), "all", ct);
    }

    public async Task<Application.Common.Models.PagedResult<UserPublicDto>> GetPublicPagedAsync(int pageNumber, int pageSize, string? role = null, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;
        Expression<Func<User, bool>>? predicate = null;
        if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, true, out var userRole))
        {
            if (userRole == UserRole.Admin) return new Application.Common.Models.PagedResult<UserPublicDto>(new List<UserPublicDto>(), 0, pageNumber, pageSize);
            predicate = u => u.Role == userRole && u.IsEmailVerified;
        }
        else
        {
            predicate = u => u.Role != UserRole.Admin && u.IsEmailVerified;
        }

        var result = await _userRepository.GetPagedAsync(pageNumber, pageSize, predicate, q => q.OrderBy(u => u.Name), ct);
        var dtos = _mapper.Map<List<UserPublicDto>>(result.Items);
        return new Application.Common.Models.PagedResult<UserPublicDto>(dtos, result.TotalCount, pageNumber, pageSize);
    }

    public async Task<UserPublicDto?> GetPublicByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user == null) return null;
        var dto = _mapper.Map<UserPublicDto>(user);
        if (user.TeamId.HasValue)
        {
            var team = await _teamRepository.GetByIdAsync(user.TeamId.Value, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, ct);
            if (team != null)
            {
                var player = team.Players.FirstOrDefault(p => p.UserId == id);
                dto.TeamName = team.Name;
                dto.TeamRole = player?.TeamRole.ToString();
            }
        }
        return dto;
    }


    /// <summary>
    /// Creates a new admin user. Role is always forced to Admin.
    /// </summary>
    public async Task<UserDto> CreateAdminAsync(CreateAdminRequest request, Guid createdByAdminId, CancellationToken ct = default)
    {
        // SYSTEM SETTING CHECK: Block admin creation during maintenance
        if (await _systemSettingsService.IsMaintenanceModeEnabledAsync(ct))
        {
            throw new BadRequestException("لا يمكن إنشاء مشرفين جدد أثناء وضع الصيانة");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("البريد الإلكتروني مطلوب");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("الاسم مطلوب");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new BadRequestException("كلمة المرور مطلوبة ويجب أن تكون 6 أحرف على الأقل");
        }

        var email = request.Email.Trim().ToLower();

        // Check email uniqueness (including soft-deleted)
        var existingUser = await _userRepository.FindAsync(u => u.Email.ToLower() == email, true, ct);
        if (existingUser != null && existingUser.Any())
        {
            throw new ConflictException("البريد الإلكتروني مستخدم بالفعل");
        }

        var newAdmin = new User
        {
            Email = email,
            Name = request.Name.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Admin, // FORCE Admin role - cannot be overridden
            Status = request.Status,
            IsEmailVerified = true, // Manually created admins are trusted/verified
            DisplayId = "ADM-" + new Random().Next(1000, 9999)
        };

        await _userRepository.AddAsync(newAdmin, ct);

        // Log activity
        try
        {
            await _analyticsService.LogActivityByTemplateAsync(
                "ADMIN_CREATED", // Add to constants
                new Dictionary<string, string> { { "adminName", newAdmin.Name } },
                createdByAdminId,
                "إدارة"
            , ct);
        }
        catch
        {
            // Don't fail if analytics logging fails
        }

        var dto = _mapper.Map<UserDto>(newAdmin);
        await _realTimeNotifier.SendUserCreatedAsync(dto, ct);
        return dto;
    }

    /// <summary>
    /// Creates a new tournament creator user. Role is always forced to TournamentCreator.
    /// </summary>
    public async Task<UserDto> CreateTournamentCreatorAsync(CreateAdminRequest request, Guid createdByAdminId, CancellationToken ct = default)
    {
        // SYSTEM SETTING CHECK: Block creation during maintenance
        if (await _systemSettingsService.IsMaintenanceModeEnabledAsync(ct))
        {
            throw new BadRequestException("لا يمكن إنشاء مستخدمين جدد أثناء وضع الصيانة");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new BadRequestException("البريد الإلكتروني مطلوب");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("الاسم مطلوب");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new BadRequestException("كلمة المرور مطلوبة ويجب أن تكون 6 أحرف على الأقل");
        }

        var email = request.Email.Trim().ToLower();

        // Check email uniqueness (including soft-deleted)
        var existingUser = await _userRepository.FindAsync(u => u.Email.ToLower() == email, true, ct);
        if (existingUser != null && existingUser.Any())
        {
            throw new ConflictException("البريد الإلكتروني مستخدم بالفعل");
        }

        var newCreator = new User
        {
            Email = email,
            Name = request.Name.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.TournamentCreator, // FORCE TournamentCreator role
            Status = request.Status,
            IsEmailVerified = true,
            DisplayId = "CRT-" + new Random().Next(1000, 9999)
        };

        await _userRepository.AddAsync(newCreator, ct);

        // Log activity
        try
        {
            await _analyticsService.LogActivityByTemplateAsync(
                "USER_CREATED",
                new Dictionary<string, string> { { "userName", newCreator.Name }, { "role", "منشئ بطولة" } },
                createdByAdminId,
                "إدارة"
            , ct);
        }
        catch { }

        var dto = _mapper.Map<UserDto>(newCreator);
        await _realTimeNotifier.SendUserCreatedAsync(dto, ct);
        return dto;
    }



    /// <summary>
    /// Gets the count of active admin users and checks if a specific user is the last admin.
    /// </summary>
    public async Task<AdminCountDto> GetAdminCountAsync(Guid? userId = null, CancellationToken ct = default)
    {
        var admins = await _userRepository.FindAsync(u =>
            u.Role == UserRole.Admin &&
            u.Status != UserStatus.Suspended &&
            u.IsEmailVerified, ct);

        var adminList = admins.ToList();
        var totalAdmins = adminList.Count;

        var isLastAdmin = false;
        if (userId.HasValue && totalAdmins == 1)
        {
            isLastAdmin = adminList.Any(a => a.Id == userId.Value);
        }

        return new AdminCountDto
        {
            TotalAdmins = totalAdmins,
            IsLastAdmin = isLastAdmin
        };
    }

    public async Task<IEnumerable<string>> GetGovernoratesAsync(CancellationToken ct = default)
    {
        return await _userRepository.GetDistinctAsync(_ => true, u => u.Governorate);
    }

    public async Task<IEnumerable<string>> GetCitiesAsync(string governorate, CancellationToken ct = default)
    {
        return await _userRepository.GetDistinctAsync(u => u.Governorate == governorate, u => u.City);
    }

    public async Task<IEnumerable<string>> GetDistrictsAsync(string city, CancellationToken ct = default)
    {
        return await _userRepository.GetDistinctAsync(u => u.City == city, u => u.Neighborhood);
    }

    public async Task<string> UploadAvatarAsync(Guid userId, System.IO.Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null) throw new NotFoundException(nameof(User), userId);

        var avatarUrl = await _fileStorage.SaveFileAsync(stream, fileName, contentType, ct);

        // Update user avatar
        user.Avatar = avatarUrl;
        await _userRepository.UpdateAsync(user, ct);
        await _userCache.InvalidateUserAsync(userId, ct);

        // Log activity (Fire and forget via background logger inside service)
        await _analyticsService.LogActivityByTemplateAsync("AVATAR_UPDATED", new Dictionary<string, string>(), userId, "مستخدم", ct);

        return avatarUrl;
    }
}
