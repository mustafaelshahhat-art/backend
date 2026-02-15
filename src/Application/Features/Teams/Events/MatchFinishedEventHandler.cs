using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Events;
using Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Teams.Events;

public class MatchFinishedEventHandler : INotificationHandler<MatchFinishedEvent>
{
    private readonly IRepository<TeamStats> _statsRepository;
    private readonly ILogger<MatchFinishedEventHandler> _logger;

    public MatchFinishedEventHandler(IRepository<TeamStats> statsRepository, ILogger<MatchFinishedEventHandler> logger)
    {
        _statsRepository = statsRepository;
        _logger = logger;
    }

    public async Task Handle(MatchFinishedEvent notification, CancellationToken cancellationToken)
    {
        var match = notification.Match;
        _logger.LogInformation("Updating team stats for match {MatchId}", match.Id);

        await UpdateTeamStats(match.HomeTeamId, match.HomeScore, match.AwayScore, cancellationToken);
        await UpdateTeamStats(match.AwayTeamId, match.AwayScore, match.HomeScore, cancellationToken);
    }

    private async Task UpdateTeamStats(Guid teamId, int teamScore, int opponentScore, CancellationToken ct)
    {
        var stats = (await _statsRepository.FindAsync(s => s.TeamId == teamId, ct)).FirstOrDefault();
        
        if (stats == null)
        {
            stats = new TeamStats { TeamId = teamId };
            stats.UpdateFromMatch(teamScore, opponentScore);
            await _statsRepository.AddAsync(stats, ct);
        }
        else
        {
            stats.UpdateFromMatch(teamScore, opponentScore);
            await _statsRepository.UpdateAsync(stats, ct);
        }
    }
}
