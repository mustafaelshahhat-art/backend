using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

public class UserCacheModel
{
    public int TokenVersion { get; set; }
    public UserStatus Status { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public interface IUserCacheService
{
    Task<UserCacheModel?> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task SetUserAsync(Guid userId, User user, CancellationToken ct = default);
    Task InvalidateUserAsync(Guid userId, CancellationToken ct = default);
}
