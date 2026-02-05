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
    private readonly IMapper _mapper;

    public UserService(IRepository<User> userRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _mapper = mapper;
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
        return _mapper.Map<UserDto>(user);
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
    }

    public async Task SuspendAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) throw new NotFoundException(nameof(User), id);

        user.Status = UserStatus.Suspended;
        await _userRepository.UpdateAsync(user);
    }

    public async Task ActivateAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) throw new NotFoundException(nameof(User), id);

        user.Status = UserStatus.Active;
        await _userRepository.UpdateAsync(user);
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
