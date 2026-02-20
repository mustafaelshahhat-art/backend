using Application.DTOs.Users;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Users.Queries.GetPublicUserById;

public class GetPublicUserByIdQueryHandler : IRequestHandler<GetPublicUserByIdQuery, UserPublicDto?>
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<Player> _playerRepository;
    private readonly IMapper _mapper;

    public GetPublicUserByIdQueryHandler(
        IRepository<User> userRepository,
        IRepository<Team> teamRepository,
        IRepository<Player> playerRepository,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _mapper = mapper;
    }

    public async Task<UserPublicDto?> Handle(GetPublicUserByIdQuery request, CancellationToken ct)
    {
        var id = request.Id;

        var user = await _userRepository.GetByIdNoTrackingAsync(id,
            new System.Linq.Expressions.Expression<Func<User, object>>[] { u => u.GovernorateNav!, u => u.CityNav! }, ct);

        if (user == null) return null;

        var dto = _mapper.Map<UserPublicDto>(user);

        if (user.TeamId.HasValue)
        {
            // Load team without expensive Players collection
            var team = await _teamRepository.GetByIdAsync(user.TeamId.Value, ct);

            if (team != null)
            {
                dto.TeamName = team.Name;
                // Targeted query for the specific player's role instead of loading all players
                var players = await _playerRepository.FindAsync(p => p.UserId == id && p.TeamId == user.TeamId.Value, ct);
                var player = players.FirstOrDefault();
                dto.TeamRole = player?.TeamRole.ToString();
            }
        }

        return dto;
    }
}
