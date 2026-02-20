using Application.Features.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;

namespace Application.Features.Tournaments.Commands.ProcessAutomatedEvents;

public class ProcessAutomatedEventsCommandHandler : IRequestHandler<ProcessAutomatedEventsCommand, Unit>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;

    public ProcessAutomatedEventsCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
    }

    public async Task<Unit> Handle(ProcessAutomatedEventsCommand request, CancellationToken cancellationToken)
    {
        // 1. Close Registration for Expired Deadlines — batch update
        var openTournaments = await _tournamentRepository.FindAsync(
            t => t.Status == TournamentStatus.RegistrationOpen && t.RegistrationDeadline < DateTime.UtcNow,
            new[] { "Registrations" }, cancellationToken);

        foreach (var t in openTournaments)
        {
            t.ChangeStatus(TournamentStatus.RegistrationClosed);
        }
        if (openTournaments.Any())
            await _tournamentRepository.UpdateRangeAsync(openTournaments, cancellationToken);

        // 2. Start Tournament for Scheduled Start Dates
        var readyTournaments = await _tournamentRepository.FindAsync(
            t => t.Status == TournamentStatus.RegistrationClosed && t.StartDate <= DateTime.UtcNow,
            new[] { "Registrations" }, cancellationToken);

        // PERF-FIX: Batch-load all matches for all ready tournaments in a single query
        var readyTournamentIds = readyTournaments.Select(t => t.Id).ToList();
        var allExistingMatches = readyTournamentIds.Any()
            ? await _matchRepository.FindAsync(m => readyTournamentIds.Contains(m.TournamentId), cancellationToken)
            : Enumerable.Empty<Match>();
        var matchesByTournament = allExistingMatches.GroupBy(m => m.TournamentId).ToDictionary(g => g.Key, g => g.ToList());

        var allNewMatches = new List<Match>();
        var tournamentsToUpdate = new List<Tournament>();

        foreach (var t in readyTournaments)
        {
            var hasExistingMatches = matchesByTournament.ContainsKey(t.Id) && matchesByTournament[t.Id].Any();

            if (t.SchedulingMode == SchedulingMode.Random && !hasExistingMatches)
            {
                var registrations = t.Registrations.Where(r => r.Status == RegistrationStatus.Approved).ToList();
                if (registrations.Count >= (t.MinTeams ?? 2) && t.HasOpeningTeams)
                {
                    var teamIds = registrations.Select(r => r.TeamId).ToList();
                    var matches = TournamentHelper.CreateMatches(t, teamIds);
                    allNewMatches.AddRange(matches);
                }
                else
                {
                    continue;
                }
            }

            t.ChangeStatus(TournamentStatus.Active);
            tournamentsToUpdate.Add(t);
        }

        // PERF-FIX: Batch insert all new matches + batch update all tournaments
        if (allNewMatches.Any())
            await _matchRepository.AddRangeAsync(allNewMatches);
        if (tournamentsToUpdate.Any())
            await _tournamentRepository.UpdateRangeAsync(tournamentsToUpdate, cancellationToken);

        return Unit.Value;
    }
}
