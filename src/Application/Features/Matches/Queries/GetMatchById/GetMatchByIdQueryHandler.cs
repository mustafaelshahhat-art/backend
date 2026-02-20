using Application.DTOs.Matches;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Matches.Queries.GetMatchById;

public class GetMatchByIdQueryHandler : IRequestHandler<GetMatchByIdQuery, MatchDto?>
{
    private readonly IRepository<Match> _matchRepository;

    public GetMatchByIdQueryHandler(IRepository<Match> matchRepository)
    {
        _matchRepository = matchRepository;
    }

    public async Task<MatchDto?> Handle(GetMatchByIdQuery request, CancellationToken cancellationToken)
    {
        // PERF: Single projected query — AsNoTracking, no double-load, no AutoMapper
        var dto = await _matchRepository.ExecuteFirstOrDefaultAsync(
            _matchRepository.GetQueryable()
            .Where(m => m.Id == request.Id)
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
                Events = m.Events.Select(e => new MatchEventDto
                {
                    Id = e.Id,
                    Type = e.Type.ToString(),
                    TeamId = e.TeamId,
                    PlayerId = e.PlayerId,
                    PlayerName = e.Player != null ? e.Player.Name : null,
                    Minute = e.Minute
                }).ToList()
            }), cancellationToken);

        return dto;
    }
}
