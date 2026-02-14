using Domain.Interfaces;

namespace Domain.Events;

public class TournamentRegistrationApprovedEvent : IDomainEvent
{
    public Guid TournamentId { get; }
    public Guid TeamId { get; }
    public Guid CaptainUserId { get; }
    public string TournamentName { get; }
    public string TeamName { get; }
    public DateTime OccurredOn { get; }

    public TournamentRegistrationApprovedEvent(Guid tournamentId, Guid teamId, Guid captainUserId, string tournamentName, string teamName)
    {
        TournamentId = tournamentId;
        TeamId = teamId;
        CaptainUserId = captainUserId;
        TournamentName = tournamentName;
        TeamName = teamName;
        OccurredOn = DateTime.UtcNow;
    }
}
