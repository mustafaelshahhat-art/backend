using System;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface ITournamentLifecycleService
{
    Task CheckAndFinalizeTournamentAsync(Guid tournamentId);
}
