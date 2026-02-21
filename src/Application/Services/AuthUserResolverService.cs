using Application.DTOs.Users;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Services;

/// <summary>
/// Resolves User entities into UserDto with team/player info.
/// Replaces the static AuthUserMapper helper, reducing 3 deps (TeamRepo, PlayerRepo, IMapper)
/// to 1 injectable service across auth handlers.
/// </summary>
public class AuthUserResolverService : IAuthUserResolverService
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<Player> _playerRepository;
    private readonly IMapper _mapper;

    public AuthUserResolverService(
        IRepository<Team> teamRepository,
        IRepository<Player> playerRepository,
        IMapper mapper)
    {
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _mapper = mapper;
    }

    public async Task<UserDto> ResolveUserWithTeamAsync(User user, CancellationToken ct = default)
    {
        var dto = _mapper.Map<UserDto>(user);
        if (user.TeamId.HasValue)
        {
            var team = await _teamRepository.GetByIdAsync(user.TeamId.Value, ct);
            if (team != null)
            {
                dto.TeamName = team.Name;
                var player = (await _playerRepository.FindAsync(
                    p => p.TeamId == user.TeamId.Value && p.UserId == user.Id, ct)).FirstOrDefault();
                dto.TeamRole = player?.TeamRole.ToString();
            }
        }
        return dto;
    }

    /// <inheritdoc />
    public UserDto MapUserDtoWithoutTeam(User user)
    {
        return _mapper.Map<UserDto>(user);
    }
}
