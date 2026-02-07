using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Users;
using Application.Interfaces;
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAnalyticsService _analyticsService;
    private readonly ISystemSettingsService _systemSettingsService;

    public UserService(
        IRepository<User> userRepository, 
        IRepository<Activity> activityRepository,
        IRepository<Team> teamRepository,
        IMapper mapper,
        IRealTimeNotifier realTimeNotifier,
        IPasswordHasher passwordHasher,
        IAnalyticsService analyticsService,
        ISystemSettingsService systemSettingsService)
    {
        _userRepository = userRepository;
        _activityRepository = activityRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
        _passwordHasher = passwordHasher;
        _analyticsService = analyticsService;
        _systemSettingsService = systemSettingsService;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _userRepository.GetAllAsync();
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
        if (!string.IsNullOrEmpty(request.Avatar)) user.Avatar = request.Avatar;
        if (!string.IsNullOrEmpty(request.City)) user.City = request.City;
        if (!string.IsNullOrEmpty(request.Governorate)) user.Governorate = request.Governorate;
        if (!string.IsNullOrEmpty(request.Neighborhood)) user.Neighborhood = request.Neighborhood;
        if (request.Age.HasValue) user.Age = request.Age;

        await _userRepository.UpdateAsync(user);
        return _mapper.Map<UserDto>(user);
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
    }

    public async Task ActivateAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) throw new NotFoundException(nameof(User), id);

        user.Status = UserStatus.Active;
        await _userRepository.UpdateAsync(user);
        await _realTimeNotifier.SendAccountStatusChangedAsync(id, UserStatus.Active.ToString());
    }

    public async Task<IEnumerable<UserDto>> GetByRoleAsync(string role)
    {
        if (Enum.TryParse<UserRole>(role, true, out var userRole))
        {
            var users = await _userRepository.FindAsync(u => u.Role == userRole);
            return _mapper.Map<IEnumerable<UserDto>>(users);
        }
        return new List<UserDto>();
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
            DisplayId = "ADM-" + new Random().Next(1000, 9999)
        };

        await _userRepository.AddAsync(newAdmin);

        // Log activity
        try
        {
            await _analyticsService.LogActivityAsync(
                "Admin Created", 
                $"Admin created new admin: {newAdmin.Name}", 
                createdByAdminId, 
                "System"
            );
        }
        catch
        {
            // Don't fail if analytics logging fails
        }

        return _mapper.Map<UserDto>(newAdmin);
    }

    /// <summary>
    /// Gets the count of active admin users and checks if a specific user is the last admin.
    /// </summary>
    public async Task<AdminCountDto> GetAdminCountAsync(Guid? userId = null)
    {
        var admins = await _userRepository.FindAsync(u => 
            u.Role == UserRole.Admin && 
            u.Status != UserStatus.Suspended);
        
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
}

