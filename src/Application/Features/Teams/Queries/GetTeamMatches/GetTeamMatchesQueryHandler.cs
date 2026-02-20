using Application.Common.Models;
using Application.DTOs.Matches;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamMatches;

public class GetTeamMatchesQueryHandler : IRequestHandler<GetTeamMatchesQuery, PagedResult<MatchDto>>
{
    private readonly IMatchRepository _matchRepository;
    public GetTeamMatchesQueryHandler(IMatchRepository matchRepository) => _matchRepository = matchRepository;

    public async Task<PagedResult<MatchDto>> Handle(GetTeamMatchesQuery request, CancellationToken ct)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var query = _matchRepository.GetQueryable()
            .Where(m => m.HomeTeamId == request.TeamId || m.AwayTeamId == request.TeamId);

        var totalCount = await _matchRepository.ExecuteCountAsync(query, ct);

        var projected = await _matchRepository.ExecuteQueryAsync(query
            .OrderByDescending(m => m.Date)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MatchDto
            {
                Id = m.Id, TournamentId = m.TournamentId,
                HomeTeamId = m.HomeTeamId, HomeTeamName = m.HomeTeam != null ? m.HomeTeam.Name : string.Empty,
                AwayTeamId = m.AwayTeamId, AwayTeamName = m.AwayTeam != null ? m.AwayTeam.Name : string.Empty,
                HomeScore = m.HomeScore, AwayScore = m.AwayScore,
                GroupId = m.GroupId, RoundNumber = m.RoundNumber, StageName = m.StageName,
                Status = m.Status.ToString(), Date = m.Date,
                TournamentName = m.Tournament != null ? m.Tournament.Name : null,
                TournamentCreatorId = m.Tournament != null ? m.Tournament.CreatorUserId : (Guid?)null
            }), ct);

        return new PagedResult<MatchDto>(projected, totalCount, request.Page, pageSize);
    }
}
