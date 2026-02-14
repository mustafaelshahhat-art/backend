using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Users;
using Domain.Entities;

namespace Application.Interfaces;

public interface IUserService
{
    Task<Application.Common.Models.PagedResult<UserDto>> GetPagedAsync(int pageNumber, int pageSize, string? role = null, CancellationToken ct = default);
    Task<Application.Common.Models.PagedResult<UserPublicDto>> GetPublicPagedAsync(int pageNumber, int pageSize, string? role = null, CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserPublicDto?> GetPublicByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SuspendAsync(Guid id, CancellationToken ct = default);
    Task ActivateAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// Creates a new admin user. Only callable by existing admins.
    /// Role is forced to Admin regardless of input.
    /// </summary>
    Task<UserDto> CreateAdminAsync(CreateAdminRequest request, Guid createdByAdminId, CancellationToken ct = default);
    Task<UserDto> CreateTournamentCreatorAsync(CreateAdminRequest request, Guid createdByAdminId, CancellationToken ct = default);


    
    /// <summary>
    /// Gets the total count of active admin users and checks if a specific user is the last admin.
    /// Used for safety checks before delete/suspend operations.
    /// </summary>
    Task<AdminCountDto> GetAdminCountAsync(Guid? userId = null, CancellationToken ct = default);

    // Location-based Discovery
    Task<IEnumerable<string>> GetGovernoratesAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetCitiesAsync(string governorate, CancellationToken ct = default);
    Task<IEnumerable<string>> GetDistrictsAsync(string city, CancellationToken ct = default);

    
    // Profile Management
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken ct = default);
    Task<string> UploadAvatarAsync(Guid userId, UploadAvatarRequest request, CancellationToken ct = default);
}

