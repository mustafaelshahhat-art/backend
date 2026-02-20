using Application.Common.Models;
using Application.DTOs.Matches;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Matches.Queries.GetMatchesByTournament;

public class GetMatchesByTournamentQueryHandler : IRequestHandler<GetMatchesByTournamentQuery, PagedResult<MatchDto>>
{
    private readonly IRepository<Match> _matchRepository;

    public GetMatchesByTournamentQueryHandler(IRepository<Match> matchRepository)
        => _matchRepository = matchRepository;

    public async Task<PagedResult<MatchDto>> Handle(GetMatchesByTournamentQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(request.PageSize, 500);

        var query = _matchRepository.GetQueryable()
            .Where(m => m.TournamentId == request.TournamentId);

        var totalCount = await _matchRepository.ExecuteCountAsync(query, cancellationToken);

        // PERF: Select() projection — skip Events collection (~80% payload reduction for tournament matches tab)
        var projected = await _matchRepository.ExecuteQueryAsync(query
            .OrderByDescending(m => m.Date)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MatchDto
            {
                Id = m.Id,
                TournamentId = m.TournamentId,
                HomeTeamId = m.HomeTeamId,
                HomeTeamName = m.HomeTeam != null ? m.HomeTeam.Name : string.Empty,
                AwayTeamId = m.AwayTeamId,
                AwayTeamName = m.AwayTeam != null ? m.AwayTeam.Name : string.Empty,
                HomeScore = m.HomeScore,
                AwayScore = m.AwayScore,
                GroupId = m.GroupId,
                RoundNumber = m.RoundNumber,
                StageName = m.StageName,
                Status = m.Status.ToString(),
                Date = m.Date,
                TournamentName = m.Tournament != null ? m.Tournament.Name : null,
                TournamentCreatorId = m.Tournament != null ? m.Tournament.CreatorUserId : (Guid?)null,
                
                // Include ONLY Goal events so the frontend can calculate top scorers
                // We project directly here to avoid N+1 issues and keep payload light
                Events = m.Events
                    .Where(e => e.Type == Domain.Enums.MatchEventType.Goal)
                    .Select(e => new MatchEventDto 
                    {
                        Id = e.Id,
                        Type = e.Type.ToString(),
                        TeamId = e.TeamId,
                        PlayerId = e.PlayerId,
                        PlayerName = e.Player != null ? e.Player.Name : null,
                        Minute = e.Minute
                    }).ToList()
            }), cancellationToken);

        return new PagedResult<MatchDto>(projected, totalCount, request.Page, pageSize);
    }
}
