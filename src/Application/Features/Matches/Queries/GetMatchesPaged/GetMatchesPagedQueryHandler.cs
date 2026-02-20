using Application.Common.Models;
using Application.DTOs.Matches;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Matches.Queries.GetMatchesPaged;

public class GetMatchesPagedQueryHandler : IRequestHandler<GetMatchesPagedQuery, PagedResult<MatchDto>>
{
    private readonly IRepository<Match> _matchRepository;

    public GetMatchesPagedQueryHandler(IRepository<Match> matchRepository)
        => _matchRepository = matchRepository;

    public async Task<PagedResult<MatchDto>> Handle(GetMatchesPagedQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize > 100 ? 100 : request.PageSize;

        var query = _matchRepository.GetQueryable();

        // Parse status string to enum if provided
        MatchStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<MatchStatus>(request.Status, true, out var s))
            parsedStatus = s;

        if (request.CreatorId.HasValue)
            query = query.Where(m => m.Tournament!.CreatorUserId == request.CreatorId.Value);
        if (parsedStatus.HasValue)
            query = query.Where(m => m.Status == parsedStatus.Value);
        if (request.TeamId.HasValue)
            query = query.Where(m => m.HomeTeamId == request.TeamId.Value || m.AwayTeamId == request.TeamId.Value);

        var totalCount = await _matchRepository.ExecuteCountAsync(query, cancellationToken);

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
                TournamentCreatorId = m.Tournament != null ? m.Tournament.CreatorUserId : (Guid?)null
            }), cancellationToken);

        return new PagedResult<MatchDto>(projected, totalCount, request.Page, pageSize);
    }
}
