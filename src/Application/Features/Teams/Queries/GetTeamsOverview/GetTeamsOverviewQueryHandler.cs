using Application.DTOs.Teams;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamsOverview;

public class GetTeamsOverviewQueryHandler : IRequestHandler<GetTeamsOverviewQuery, TeamsOverviewDto>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<TeamJoinRequest> _joinRequestRepository;

    public GetTeamsOverviewQueryHandler(IRepository<Team> teamRepository, IRepository<TeamJoinRequest> joinRequestRepository)
    {
        _teamRepository = teamRepository;
        _joinRequestRepository = joinRequestRepository;
    }

    public async Task<TeamsOverviewDto> Handle(GetTeamsOverviewQuery request, CancellationToken ct)
    {
        var rawTeams = await _teamRepository.ExecuteQueryAsync(
            _teamRepository.GetQueryable()
            .Where(t => t.Players.Any(p => p.UserId == request.UserId))
            .Select(t => new
            {
                t.Id, t.Name,
                CaptainName = t.Players.Where(p => p.TeamRole == TeamRole.Captain).Select(p => p.Name).FirstOrDefault() ?? string.Empty,
                t.Founded, t.City, t.IsActive,
                PlayerCount = t.Players.Count,
                IsCaptain = t.Players.Any(p => p.UserId == request.UserId && p.TeamRole == TeamRole.Captain),
                HasStats = t.Statistics != null,
                MatchesPlayed = t.Statistics != null ? t.Statistics.MatchesPlayed : 0,
                Wins = t.Statistics != null ? t.Statistics.Wins : 0,
                Draws = t.Statistics != null ? t.Statistics.Draws : 0,
                Losses = t.Statistics != null ? t.Statistics.Losses : 0,
                GoalsFor = t.Statistics != null ? t.Statistics.GoalsFor : 0,
                GoalsAgainst = t.Statistics != null ? t.Statistics.GoalsAgainst : 0
            }), ct);

        var allUserTeams = rawTeams.Select(t => new
        {
            t.IsCaptain,
            Dto = new TeamDto
            {
                Id = t.Id, Name = t.Name, CaptainName = t.CaptainName,
                Founded = t.Founded, City = t.City, IsActive = t.IsActive,
                PlayerCount = t.PlayerCount, MaxPlayers = 10,
                IsComplete = t.PlayerCount >= Team.MinPlayersForCompletion,
                Stats = t.HasStats ? new TeamStatsDto
                {
                    Matches = t.MatchesPlayed, Wins = t.Wins, Draws = t.Draws,
                    Losses = t.Losses, GoalsFor = t.GoalsFor, GoalsAgainst = t.GoalsAgainst
                } : null
            }
        }).ToList();

        var invitationDtos = await _joinRequestRepository.ExecuteQueryAsync(
            _joinRequestRepository.GetQueryable()
            .Where(r => r.UserId == request.UserId && r.Status == "pending")
            .Select(r => new JoinRequestDto
            {
                Id = r.Id, TeamId = r.TeamId,
                TeamName = r.Team != null ? r.Team.Name : string.Empty,
                PlayerId = r.UserId,
                PlayerName = r.User != null ? r.User.Name : string.Empty,
                RequestDate = r.CreatedAt, Status = r.Status,
                InitiatedByPlayer = r.InitiatedByPlayer
            }), ct);

        return new TeamsOverviewDto
        {
            OwnedTeams = allUserTeams.Where(t => t.IsCaptain).Select(t => t.Dto).ToList(),
            MemberTeams = allUserTeams.Where(t => !t.IsCaptain).Select(t => t.Dto).ToList(),
            PendingInvitations = invitationDtos
        };
    }
}
