using System;
using System.Threading.Tasks;

namespace Application.Interfaces;

public interface ITournamentLifecycleService
{
    Task CheckAndFinalizeTournamentAsync(Guid tournamentId);
    Task GenerateKnockoutR1Async(Guid tournamentId);
    List<Application.DTOs.Tournaments.TournamentStandingDto> CalculateStandings(IEnumerable<Domain.Entities.Match> allMatches, IEnumerable<Domain.Entities.TeamRegistration> teams);
}
