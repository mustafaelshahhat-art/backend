using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Services;

/// <summary>
/// Parameter object grouping Tournament, TeamRegistration repositories and DistributedLock.
/// These three are always used together in tournament registration flow handlers.
/// </summary>
public class TournamentRegistrationContext : ITournamentRegistrationContext
{
    public IRepository<Tournament> Tournaments { get; }
    public IRepository<TeamRegistration> Registrations { get; }
    public IDistributedLock DistributedLock { get; }

    public TournamentRegistrationContext(
        IRepository<Tournament> tournaments,
        IRepository<TeamRegistration> registrations,
        IDistributedLock distributedLock)
    {
        Tournaments = tournaments;
        Registrations = registrations;
        DistributedLock = distributedLock;
    }
}
