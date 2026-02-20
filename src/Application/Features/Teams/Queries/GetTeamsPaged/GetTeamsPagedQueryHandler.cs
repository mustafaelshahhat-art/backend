using Application.Common.Models;
using Application.DTOs.Teams;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamsPaged;

public class GetTeamsPagedQueryHandler : IRequestHandler<GetTeamsPagedQuery, PagedResult<TeamDto>>
{
    private readonly IRepository<Team> _teamRepository;
    public GetTeamsPagedQueryHandler(IRepository<Team> teamRepository) => _teamRepository = teamRepository;

    public async Task<PagedResult<TeamDto>> Handle(GetTeamsPagedQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var query = _teamRepository.GetQueryable();

        if (request.CaptainId.HasValue)
            query = query.Where(t => t.Players.Any(p => p.TeamRole == TeamRole.Captain && p.UserId == request.CaptainId.Value));
        else if (request.PlayerId.HasValue)
            query = query.Where(t => t.Players.Any(p => p.UserId == request.PlayerId.Value));

        var totalCount = await _teamRepository.ExecuteCountAsync(query, ct);

        var projected = await _teamRepository.ExecuteQueryAsync(query
            .OrderBy(t => t.Name)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TeamDto
            {
                Id = t.Id, Name = t.Name, Founded = t.Founded, City = t.City, IsActive = t.IsActive,
                PlayerCount = t.Players.Count, MaxPlayers = 10,
                CaptainName = t.Players.Where(p => p.TeamRole == TeamRole.Captain).Select(p => p.Name).FirstOrDefault() ?? string.Empty,
                Stats = t.Statistics != null ? new TeamStatsDto
                {
                    Matches = t.Statistics.MatchesPlayed, Wins = t.Statistics.Wins, Draws = t.Statistics.Draws,
                    Losses = t.Statistics.Losses, GoalsFor = t.Statistics.GoalsFor, GoalsAgainst = t.Statistics.GoalsAgainst
                } : null
            }), ct);

        return new PagedResult<TeamDto>(projected, totalCount, request.Page, pageSize);
    }
}
