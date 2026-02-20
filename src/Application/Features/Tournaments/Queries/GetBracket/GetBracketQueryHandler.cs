using Application.DTOs.Matches;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Queries.GetBracket;

public class GetBracketQueryHandler : IRequestHandler<GetBracketQuery, BracketDto>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;

    public GetBracketQueryHandler(IRepository<Tournament> tournamentRepository, IRepository<Match> matchRepository)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
    }

    public async Task<BracketDto> Handle(GetBracketQuery request, CancellationToken cancellationToken)
    {
        // PERF: AnyAsync (EXISTS query) instead of GetByIdAsync (full SELECT *).
        // Before: SELECT * FROM Tournaments WHERE Id = @id  — loads all columns into memory
        // After:  SELECT TOP 1 1 FROM Tournaments WHERE Id = @id  — zero columns transferred
        if (!await _tournamentRepository.AnyAsync(t => t.Id == request.TournamentId, cancellationToken))
            throw new NotFoundException(nameof(Tournament), request.TournamentId);

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
