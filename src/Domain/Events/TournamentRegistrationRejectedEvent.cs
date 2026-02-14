using Domain.Interfaces;

namespace Domain.Events;

public class TournamentRegistrationRejectedEvent : IDomainEvent
{
    public Guid TournamentId { get; }
    public Guid TeamId { get; }
    public Guid CaptainUserId { get; }
    public string TournamentName { get; }
    public string TeamName { get; }
    public string Reason { get; }
    public DateTime OccurredOn { get; }

    public TournamentRegistrationRejectedEvent(Guid tournamentId, Guid teamId, Guid captainUserId, string tournamentName, string teamName, string reason)
    {
        TournamentId = tournamentId;
        TeamId = teamId;
        CaptainUserId = captainUserId;
        TournamentName = tournamentName;
        TeamName = teamName;
        Reason = reason;
        OccurredOn = DateTime.UtcNow;
    }
}
