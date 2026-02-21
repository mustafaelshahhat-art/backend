using Application.DTOs.Teams;
using Domain.Entities;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Teams.Queries.GetTeamById;

public class GetTeamByIdQueryHandler : IRequestHandler<GetTeamByIdQuery, TeamDto?>
{
    private readonly IRepository<Team> _teamRepository;

    public GetTeamByIdQueryHandler(IRepository<Team> teamRepository)
    {
        _teamRepository = teamRepository;
    }

    public async Task<TeamDto?> Handle(GetTeamByIdQuery request, CancellationToken ct)
    {
        // PERF-FIX: Server-side projection — previously loaded full Team + ALL Player
        // entities + Statistics into memory, then ran AutoMapper reflection mapping.
        // Now projects only the columns needed for the DTO at the SQL level.
        var query = _teamRepository.GetQueryable()
            .Where(t => t.Id == request.Id)
            .Select(t => new TeamDto
            {
                Id = t.Id,
                Name = t.Name,
                CaptainName = t.Players.Where(p => p.TeamRole == Domain.Enums.TeamRole.Captain)
                                       .Select(p => p.Name).FirstOrDefault() ?? string.Empty,
                Founded = t.Founded,
                City = t.City,
                IsActive = t.IsActive,
                PlayerCount = t.Players.Count,
                MaxPlayers = 10,
                IsComplete = t.Players.Count >= Team.MinPlayersForCompletion,
                Stats = t.Statistics != null ? new TeamStatsDto
                {
                    Matches = t.Statistics.MatchesPlayed,
                    Wins = t.Statistics.Wins,
                    Draws = t.Statistics.Draws,
                    Losses = t.Statistics.Losses,
                    GoalsFor = t.Statistics.GoalsFor,
                    GoalsAgainst = t.Statistics.GoalsAgainst,
                    Rank = 0
                } : null,
                Players = t.Players.Select(p => new PlayerDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    DisplayId = p.DisplayId,
                    Number = p.Number,
                    Position = p.Position,
                    Status = p.Status.ToString(),
                    Goals = p.Goals,
                    Assists = p.Assists,
                    YellowCards = p.YellowCards,
                    RedCards = p.RedCards,
                    TeamId = p.TeamId,
                    UserId = p.UserId,
                    TeamRole = p.TeamRole
                }).ToList()
            });

        return await _teamRepository.ExecuteFirstOrDefaultAsync(query, ct);
    }
}
