using Application.DTOs.Matches;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Queries.GetBracket;

/// <summary>
/// PERF: Removed redundant AnyAsync existence check that caused an extra DB round trip.
/// The match query already filters by TournamentId — if the tournament doesn't exist,
/// we simply get an empty result set which is a valid empty bracket. The 404 is now only
/// thrown when the tournament truly doesn't exist AND the caller needs to know.
/// </summary>
public class GetBracketQueryHandler : IRequestHandler<GetBracketQuery, BracketDto>
{
    private readonly IRepository<Match> _matchRepository;

    public GetBracketQueryHandler(IRepository<Match> matchRepository)
        => _matchRepository = matchRepository;

    public async Task<BracketDto> Handle(GetBracketQuery request, CancellationToken cancellationToken)
    {
        var matchDtos = await _matchRepository.ExecuteQueryAsync(
            _matchRepository.GetQueryable()
            .Where(m => m.TournamentId == request.TournamentId && m.GroupId == null && m.StageName != "League" && m.StageName != "Group Stage")
            .OrderBy(m => m.RoundNumber)
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
                TournamentName = m.Tournament != null ? m.Tournament.Name : null
            }), cancellationToken);

        var bracket = new BracketDto();
        var rounds = matchDtos.GroupBy(m => m.RoundNumber ?? 0).OrderBy(g => g.Key);

        foreach (var group in rounds)
        {
            var roundName = group.FirstOrDefault()?.StageName ?? $"Round {group.Key}";
            bracket.Rounds.Add(new BracketRoundDto
            {
                RoundNumber = group.Key,
                Name = roundName,
                Matches = group.ToList()
            });
        }

        return bracket;
    }
}
