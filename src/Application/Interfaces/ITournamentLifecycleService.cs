using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface ITournamentLifecycleService
{
    Task CheckAndFinalizeTournamentAsync(Guid tournamentId, CancellationToken ct = default);
    Task GenerateKnockoutR1Async(Guid tournamentId, CancellationToken ct = default);
    List<Application.DTOs.Tournaments.TournamentStandingDto> CalculateStandings(IEnumerable<Domain.Entities.Match> allMatches, IEnumerable<Domain.Entities.TeamRegistration> teams);
}
