using Application.DTOs.Users;
using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Resolves a User entity into a UserDto with team information.
/// Replaces the static AuthUserMapper helper to reduce handler dependencies.
/// </summary>
public interface IAuthUserResolverService
{
    Task<UserDto> ResolveUserWithTeamAsync(User user, CancellationToken ct = default);
}
