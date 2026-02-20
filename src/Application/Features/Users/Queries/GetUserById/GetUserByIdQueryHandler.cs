using Application.DTOs.Users;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Activity> _activityRepository;
    private readonly IRepository<Team> _teamRepository;

    public GetUserByIdQueryHandler(
        IRepository<User> userRepository,
        IRepository<Activity> activityRepository,
        IRepository<Team> teamRepository)
    {
        _userRepository = userRepository;
        _activityRepository = activityRepository;
        _teamRepository = teamRepository;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var id = request.Id;

        var userProjection = await _userRepository.ExecuteFirstOrDefaultAsync(
            _userRepository.GetQueryable()
            .Where(u => u.Id == id)
            .Select(u => new UserDto
            {
                Id = u.Id,
                DisplayId = u.DisplayId,
                Name = u.Name,
                Email = u.Email,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                Phone = u.Phone,
                Age = u.Age,
                NationalId = u.NationalId,
                GovernorateId = u.GovernorateId,
                GovernorateNameAr = u.GovernorateNav != null ? u.GovernorateNav.NameAr : null,
                CityId = u.CityId,
                CityNameAr = u.CityNav != null ? u.CityNav.NameAr : null,
                AreaId = u.AreaId,
                AreaNameAr = u.AreaNav != null ? u.AreaNav.NameAr : null,
                IdFrontUrl = u.IdFrontUrl,
                IdBackUrl = u.IdBackUrl,
                TeamId = u.TeamId,
                IsEmailVerified = u.IsEmailVerified,
                CreatedAt = u.CreatedAt
            }), cancellationToken);

        if (userProjection == null) return null;

        // Team info
        var teamInfo = await _teamRepository.ExecuteQueryAsync(
            _teamRepository.GetQueryable()
            .Where(t => t.Players.Any(p => p.UserId == id))
            .Select(t => new
            {
                t.Id,
                t.Name,
                PlayerRole = t.Players
                    .Where(p => p.UserId == id)
                    .Select(p => p.TeamRole)
                    .FirstOrDefault()
            }), cancellationToken);

        if (teamInfo.Count > 0)
        {
            userProjection.JoinedTeamIds = teamInfo.Select(t => t.Id).ToList();

            var primaryTeam = userProjection.TeamId.HasValue
                ? teamInfo.FirstOrDefault(t => t.Id == userProjection.TeamId.Value)
                : teamInfo.FirstOrDefault(t => t.PlayerRole == TeamRole.Captain);

            if (primaryTeam != null)
            {
                userProjection.TeamId = primaryTeam.Id;
                userProjection.TeamName = primaryTeam.Name;
                userProjection.TeamRole = primaryTeam.PlayerRole.ToString();
            }
        }

        // Activities
        userProjection.Activities = await _activityRepository.ExecuteQueryAsync(
            _activityRepository.GetQueryable()
            .Where(a => a.UserId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new UserActivityDto
            {
                Id = a.Id,
                Type = a.Type,
                Message = a.Message,
                CreatedAt = a.CreatedAt
            }), cancellationToken);

        return userProjection;
    }
}
