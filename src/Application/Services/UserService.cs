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

    public UserService(
        IRepository<User> userRepository, 
        IRepository<Activity> activityRepository,
        IRepository<Team> teamRepository,
        IMapper mapper,
        IRealTimeNotifier realTimeNotifier)
    {
        _userRepository = userRepository;
        _activityRepository = activityRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
        _realTimeNotifier = realTimeNotifier;
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
        // Soft delete logic is likely handled by repository or business rule.
        // Task says "Delete user (or deactivate)". Soft delete enabled in DbContext.
        // So Repository.DeleteAsync will mark it Deleted, and DbContext will set IsDeleted.
        await _userRepository.DeleteAsync(id);
        await _realTimeNotifier.SendAccountStatusChangedAsync(id, "Deleted");
    }

    public async Task SuspendAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) throw new NotFoundException(nameof(User), id);

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
}
