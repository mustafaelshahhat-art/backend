using Domain.Interfaces;

namespace Domain.Events;

/// <summary>
/// Raised after a team successfully registers for a tournament.
/// 
/// Subscribers handle side-effects:
/// - SignalR real-time update (tournament viewer refresh)
/// - Push notification to team captain
/// - Analytics logging
/// 
/// This replaces inline IRealTimeNotifier + IAnalyticsService calls
/// that were scattered throughout RegisterTeamAsync.
/// </summary>
public class TeamRegisteredEvent : IDomainEvent
{
    public Guid TournamentId { get; }
    public Guid TeamId { get; }
    public Guid RegistrationId { get; }
    public string RegistrationStatus { get; }
    public DateTime OccurredOn { get; }

    public TeamRegisteredEvent(
        Guid tournamentId,
        Guid teamId,
        Guid registrationId,
        string registrationStatus)
    {
        TournamentId = tournamentId;
        TeamId = teamId;
        RegistrationId = registrationId;
        RegistrationStatus = registrationStatus;
        OccurredOn = DateTime.UtcNow;
    }
}
