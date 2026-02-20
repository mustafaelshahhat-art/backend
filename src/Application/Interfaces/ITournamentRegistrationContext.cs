using Domain.Entities;
using Domain.Interfaces;

namespace Application.Interfaces;

/// <summary>
/// Parameter object that groups the three dependencies always used together
/// in tournament registration flow handlers: tournament repo, registration repo, and distributed lock.
/// </summary>
public interface ITournamentRegistrationContext
{
    IRepository<Tournament> Tournaments { get; }
    IRepository<TeamRegistration> Registrations { get; }
    IDistributedLock DistributedLock { get; }
}
