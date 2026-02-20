using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface ITournamentLifecycleService
{
    Task<Application.DTOs.Tournaments.TournamentLifecycleResult> CheckAndFinalizeTournamentAsync(Guid tournamentId, CancellationToken ct = default);
    Task<Application.DTOs.Tournaments.TournamentLifecycleResult> GenerateKnockoutR1Async(Guid tournamentId, CancellationToken ct = default);
}
