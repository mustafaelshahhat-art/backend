using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Users;
using Application.Interfaces;
using Application.Common;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;

namespace Application.Services;

public class UserService : IUserService
{
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
        ISystemSettingsService systemSettingsService)
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
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _userRepository.FindAsync(u => u.IsEmailVerified);
        return _mapper.Map<IEnumerable<UserDto>>(users);
    }

    public async Task<UserDto?> GetByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return null;

        var dto = _mapper.Map<UserDto>(user);

        // Fetch Team Name and Ownership if exists
        if (user.TeamId.HasValue)
        {
            var team = await _teamRepository.GetByIdAsync(user.TeamId.Value);
            if (team != null)
            {
                dto.TeamName = team.Name;
                dto.IsTeamOwner = team.CaptainId == id;
            }
        }

        // Fetch Recent Activities
        var activities = await _activityRepository.FindAsync(a => a.UserId == id);
        dto.Activities = _mapper.Map<List<UserActivityDto>>(activities.OrderByDescending(a => a.CreatedAt).Take(10).ToList());

        return dto;
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _userRepository.GetByIdAsync(id);
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

        await _userRepository.UpdateAsync(user);
        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto);
        return dto;
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
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

        await _userRepository.DeleteAsync(id);
        await _realTimeNotifier.SendAccountStatusChangedAsync(id, "Deleted");
        await _realTimeNotifier.SendUserDeletedAsync(id);
    }

    public async Task SuspendAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) throw new NotFoundException(nameof(User), id);

        // CRITICAL SAFETY: Prevent suspending the last admin
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await GetAdminCountAsync(id);
            if (adminCount.IsLastAdmin)
            {
                throw new BadRequestException("لا يمكن إيقاف آخر مشرف في النظام");
            }
        }

        user.Status = UserStatus.Suspended;
        await _userRepository.UpdateAsync(user);
        await _realTimeNotifier.SendAccountStatusChangedAsync(id, UserStatus.Suspended.ToString());
        
        // Persistent Notification
        await _notificationService.SendNotificationByTemplateAsync(id, NotificationTemplates.ACCOUNT_SUSPENDED);
        
        // Full Update for Lists
        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto);
        
        // Lightweight System Event
        await _realTimeNotifier.SendSystemEventAsync("USER_BLOCKED", new { UserId = id }, $"user:{id}");
    }

    public async Task ActivateAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) throw new NotFoundException(nameof(User), id);

        user.Status = UserStatus.Active;
        await _userRepository.UpdateAsync(user);
        await _realTimeNotifier.SendAccountStatusChangedAsync(id, UserStatus.Active.ToString());

        // Persistent Notification
        await _notificationService.SendNotificationByTemplateAsync(id, NotificationTemplates.ACCOUNT_APPROVED);

        // Full Update for Lists
        var dto = _mapper.Map<UserDto>(user);
        await _realTimeNotifier.SendUserUpdatedAsync(dto);

        // Lightweight System Event
        await _realTimeNotifier.SendSystemEventAsync("USER_APPROVED", new { UserId = id }, $"user:{id}");
    }

    public async Task<IEnumerable<UserDto>> GetByRoleAsync(string role)
    {
        if (Enum.TryParse<UserRole>(role, true, out var userRole))
        {
            var users = await _userRepository.FindAsync(u => u.Role == userRole && u.IsEmailVerified);
            return _mapper.Map<IEnumerable<UserDto>>(users);
        }
        return new List<UserDto>();
    }

    public async Task<IEnumerable<UserPublicDto>> GetPublicByRoleAsync(string role)
    {
        if (Enum.TryParse<UserRole>(role, true, out var userRole))
        {
            if (userRole == UserRole.Admin) return new List<UserPublicDto>();
            var users = await _userRepository.FindAsync(u => u.Role == userRole && u.IsEmailVerified);
            return _mapper.Map<IEnumerable<UserPublicDto>>(users);
        }
        return new List<UserPublicDto>();
    }

    public async Task<UserPublicDto?> GetPublicByIdAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return null;
        var dto = _mapper.Map<UserPublicDto>(user);
        if (user.TeamId.HasValue)
        {
            var team = await _teamRepository.GetByIdAsync(user.TeamId.Value);
            if (team != null)
            {
                dto.TeamName = team.Name;
                dto.IsTeamOwner = team.CaptainId == id;
            }
        }
        return dto;
    }


    /// <summary>
    /// Creates a new admin user. Role is always forced to Admin.
    /// </summary>
    public async Task<UserDto> CreateAdminAsync(CreateAdminRequest request, Guid createdByAdminId)
    {
        // SYSTEM SETTING CHECK: Block admin creation during maintenance
        if (await _systemSettingsService.IsMaintenanceModeEnabledAsync())
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

        // Check email uniqueness
        var existingUser = await _userRepository.FindAsync(u => u.Email.ToLower() == email);
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

        await _userRepository.AddAsync(newAdmin);

        // Log activity
        try
        {
            await _analyticsService.LogActivityByTemplateAsync(
                "ADMIN_CREATED", // Add to constants
                new Dictionary<string, string> { { "adminName", newAdmin.Name } }, 
                createdByAdminId, 
                "إدارة"
            );
        }
        catch
        {
            // Don't fail if analytics logging fails
        }

        var dto = _mapper.Map<UserDto>(newAdmin);
        await _realTimeNotifier.SendUserCreatedAsync(dto);
        return dto;
    }

    /// <summary>
    /// Creates a new tournament creator user. Role is always forced to TournamentCreator.
    /// </summary>
    public async Task<UserDto> CreateTournamentCreatorAsync(CreateAdminRequest request, Guid createdByAdminId)
    {
        // SYSTEM SETTING CHECK: Block creation during maintenance
        if (await _systemSettingsService.IsMaintenanceModeEnabledAsync())
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

        // Check email uniqueness
        var existingUser = await _userRepository.FindAsync(u => u.Email.ToLower() == email);
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

        await _userRepository.AddAsync(newCreator);

        // Log activity
        try
        {
            await _analyticsService.LogActivityByTemplateAsync(
                "USER_CREATED", 
                new Dictionary<string, string> { { "userName", newCreator.Name }, { "role", "منشئ بطولة" } }, 
                createdByAdminId, 
                "إدارة"
            );
        }
        catch { }

        var dto = _mapper.Map<UserDto>(newCreator);
        await _realTimeNotifier.SendUserCreatedAsync(dto);
        return dto;
    }

    /// <summary>
    /// Gets the count of active admin users and checks if a specific user is the last admin.
    /// </summary>
    public async Task<AdminCountDto> GetAdminCountAsync(Guid? userId = null)
    {
        var admins = await _userRepository.FindAsync(u => 
            u.Role == UserRole.Admin && 
            u.Status != UserStatus.Suspended &&
            u.IsEmailVerified);
        
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

    public async Task<IEnumerable<string>> GetGovernoratesAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return users.Select(u => u.Governorate).Where(g => !string.IsNullOrEmpty(g)).Distinct()!;
    }

    public async Task<IEnumerable<string>> GetCitiesAsync(string governorate)
    {
        var users = await _userRepository.FindAsync(u => u.Governorate == governorate);
        return users.Select(u => u.City).Where(c => !string.IsNullOrEmpty(c)).Distinct()!;
    }

    public async Task<IEnumerable<string>> GetDistrictsAsync(string city)
    {
        var users = await _userRepository.FindAsync(u => u.City == city);
        return users.Select(u => u.Neighborhood).Where(d => !string.IsNullOrEmpty(d)).Distinct()!;
    }

    public async Task<IEnumerable<UserDto>> GetRefereesByLocationAsync(string? district = null, string? city = null, string? governorate = null)
    {
        var referees = await _userRepository.FindAsync(u => u.Role == UserRole.Referee && u.Status == UserStatus.Active);
        
        var query = referees.AsQueryable();
        if (!string.IsNullOrEmpty(governorate)) query = query.Where(u => u.Governorate == governorate);
        if (!string.IsNullOrEmpty(city)) query = query.Where(u => u.City == city);
        if (!string.IsNullOrEmpty(district)) query = query.Where(u => u.Neighborhood == district);

        return _mapper.Map<IEnumerable<UserDto>>(query.ToList());
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _userRepository.GetByIdAsync(userId);
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
        await _userRepository.UpdateAsync(user);

        // Log activity
        try
        {
            await _analyticsService.LogActivityByTemplateAsync(
                "PASSWORD_CHANGED", 
                new Dictionary<string, string>(), 
                userId, 
                "مستخدم"
            );
        }
        catch
        {
            // Don't fail if analytics logging fails
        }
    }

    public async Task<string> UploadAvatarAsync(Guid userId, UploadAvatarRequest request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            throw new NotFoundException(nameof(User), userId);
        }

        // Validate base64 image
        if (string.IsNullOrWhiteSpace(request.Base64Image))
        {
            throw new BadRequestException("Image data is required");
        }

        // Remove data URL prefix if present
        var base64Data = request.Base64Image;
        if (base64Data.StartsWith("data:image"))
        {
            base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);
        }

        // Validate base64 format
        byte[] imageData;
        try
        {
            imageData = Convert.FromBase64String(base64Data);
        }
        catch
        {
            throw new BadRequestException("Invalid image data");
        }

        // Save file
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var fileExtension = Path.GetExtension(request.FileName) ?? ".jpg";
        var fileName = $"avatar_{userId}_{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await File.WriteAllBytesAsync(filePath, imageData);

        var avatarUrl = $"/uploads/{fileName}";

        // Update user avatar
        user.Avatar = avatarUrl;
        await _userRepository.UpdateAsync(user);

        // Log activity
        try
        {
            await _analyticsService.LogActivityByTemplateAsync(
                "AVATAR_UPDATED", 
                new Dictionary<string, string>(), 
                userId, 
                "مستخدم"
            );
        }
        catch
        {
            // Don't fail if analytics logging fails
        }

        return avatarUrl;
    }
}

