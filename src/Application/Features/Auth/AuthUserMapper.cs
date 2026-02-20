using Application.DTOs.Users;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Features.Auth;

internal static class AuthUserMapper
{
    public static async Task<UserDto> MapUserWithTeamInfoAsync(
        User user, IMapper mapper, IRepository<Team> teamRepository, 
        IRepository<Player> playerRepository, CancellationToken ct = default)
    {
        var dto = mapper.Map<UserDto>(user);
        if (user.TeamId.HasValue)
        {
            var team = await teamRepository.GetByIdAsync(user.TeamId.Value, ct);
            if (team != null)
            {
                dto.TeamName = team.Name;
                var player = (await playerRepository.FindAsync(p => p.TeamId == user.TeamId.Value && p.UserId == user.Id, ct)).FirstOrDefault();
                dto.TeamRole = player?.TeamRole.ToString();
            }
        }
        return dto;
    }
}
