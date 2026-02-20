using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Teams.Commands.DisableTeam;

public class DisableTeamCommandHandler : IRequestHandler<DisableTeamCommand, Unit>
{
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly IMatchEventNotifier _matchEventNotifier;

    public DisableTeamCommandHandler(
        IRepository<Team> teamRepository, IRepository<TeamRegistration> registrationRepository,
        IMatchRepository matchRepository, ITournamentLifecycleService lifecycleService,
        IMatchEventNotifier matchEventNotifier)
    {
        _teamRepository = teamRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _lifecycleService = lifecycleService;
        _matchEventNotifier = matchEventNotifier;
    }

    public async Task<Unit> Handle(DisableTeamCommand request, CancellationToken ct)
    {
        var team = await _teamRepository.GetByIdAsync(request.TeamId, ct);
        if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);

        team.IsActive = false;
        await _teamRepository.UpdateAsync(team, ct);

        // Handle active tournaments - withdrawal + forfeit
        var activeRegistrations = await _registrationRepository.FindAsync(
            r => r.TeamId == request.TeamId && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.PendingPaymentReview), ct);

        if (activeRegistrations.Any())
        {
            foreach (var reg in activeRegistrations) reg.Status = RegistrationStatus.Withdrawn;
            await _registrationRepository.UpdateRangeAsync(activeRegistrations, ct);

            foreach (var reg in activeRegistrations)
            {
                var matches = await _matchRepository.FindAsync(
                    m => m.TournamentId == reg.TournamentId &&
                         (m.HomeTeamId == request.TeamId || m.AwayTeamId == request.TeamId) &&
                         m.Status == MatchStatus.Scheduled, ct);

                foreach (var match in matches)
                {
                    match.Status = MatchStatus.Finished;
                    match.Forfeit = true;
                    if (match.HomeTeamId == request.TeamId) { match.HomeScore = 0; match.AwayScore = 3; }
                    else { match.HomeScore = 3; match.AwayScore = 0; }
                }
                await _matchRepository.UpdateRangeAsync(matches, ct);
                var lifecycleResult = await _lifecycleService.CheckAndFinalizeTournamentAsync(reg.TournamentId, ct);
                await _matchEventNotifier.HandleLifecycleOutcomeAsync(lifecycleResult, ct);
            }
        }

        // Notify captain
        var captain = team.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain?.UserId.HasValue == true)
        {
            await _matchEventNotifier.SendNotificationAsync(captain.UserId.Value,
                "تم إيقاف فريقك", "تم إيقاف فريقك من قبل إدارة النظام",
                NotificationCategory.Team, ct: ct);
        }

        return Unit.Value;
    }
}
