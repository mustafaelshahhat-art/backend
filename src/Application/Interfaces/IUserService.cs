using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Users;
using Domain.Entities;

namespace Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<UserDto?> GetByIdAsync(Guid id);
    Task<UserPublicDto?> GetPublicByIdAsync(Guid id);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request);
    Task DeleteAsync(Guid id);
    Task SuspendAsync(Guid id);
    Task ActivateAsync(Guid id);
    Task<IEnumerable<UserDto>> GetByRoleAsync(string role);
    Task<IEnumerable<UserPublicDto>> GetPublicByRoleAsync(string role);
    
    /// <summary>
    /// Creates a new admin user. Only callable by existing admins.
    /// Role is forced to Admin regardless of input.
    /// </summary>
    Task<UserDto> CreateAdminAsync(CreateAdminRequest request, Guid createdByAdminId);
    
    /// <summary>
    /// Gets the total count of active admin users and checks if a specific user is the last admin.
    /// Used for safety checks before delete/suspend operations.
    /// </summary>
    Task<AdminCountDto> GetAdminCountAsync(Guid? userId = null);

    // Location-based Discovery
    Task<IEnumerable<string>> GetGovernoratesAsync();
    Task<IEnumerable<string>> GetCitiesAsync(string governorate);
    Task<IEnumerable<string>> GetDistrictsAsync(string city);
    Task<IEnumerable<UserDto>> GetRefereesByLocationAsync(string? district = null, string? city = null, string? governorate = null);
    
    // Profile Management
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task<string> UploadAvatarAsync(Guid userId, UploadAvatarRequest request);
}

