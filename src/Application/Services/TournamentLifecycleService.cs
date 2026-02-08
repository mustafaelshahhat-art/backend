using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

namespace Application.Services;

public class TournamentLifecycleService : ITournamentLifecycleService
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly INotificationService _notificationService;
    private readonly IAnalyticsService _analyticsService;

    public TournamentLifecycleService(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Team> teamRepository,
        INotificationService notificationService,
        IAnalyticsService analyticsService)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _registrationRepository = registrationRepository;
        _teamRepository = teamRepository;
        _notificationService = notificationService;
        _analyticsService = analyticsService;
    }

    public async Task CheckAndFinalizeTournamentAsync(Guid tournamentId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null) return;

        // 1. If already finished, return
        if (tournament.Status == "completed") return;

        // 2. Check all matches
        var allMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        if (!allMatches.Any()) return; // No matches yet

        bool allFinished = allMatches.All(m => m.Status == MatchStatus.Finished);
        if (!allFinished) return;

        // 3. Calculate Standings to find the winner
        // (Copying logic from TournamentService.GetStandingsAsync to avoid circular dependency)
        var finishedMatches = allMatches.Where(m => m.Status == MatchStatus.Finished).ToList();
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournamentId && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.Withdrawn));
        
        var standings = new List<TournamentStandingDto>();
        foreach (var reg in registrations)
        {
            var team = await _teamRepository.GetByIdAsync(reg.TeamId);
            standings.Add(new TournamentStandingDto
            {
                TeamId = reg.TeamId,
                TeamName = team?.Name ?? "Unknown",
                Played = 0,
                Won = 0,
                Drawn = 0,
                Lost = 0,
                GoalsFor = 0,
                GoalsAgainst = 0,
                Points = 0
            });
        }

        foreach (var match in finishedMatches)
        {
            var home = standings.FirstOrDefault(s => s.TeamId == match.HomeTeamId);
            var away = standings.FirstOrDefault(s => s.TeamId == match.AwayTeamId);

            if (home == null || away == null) continue;

            home.Played++;
            away.Played++;
            home.GoalsFor += match.HomeScore;
            home.GoalsAgainst += match.AwayScore;
            away.GoalsFor += match.AwayScore;
            away.GoalsAgainst += match.HomeScore;

            if (match.HomeScore > match.AwayScore)
            {
                home.Won++;
                home.Points += 3;
                away.Lost++;
            }
            else if (match.AwayScore > match.HomeScore)
            {
                away.Won++;
                away.Points += 3;
                home.Lost++;
            }
            else
            {
                home.Drawn++;
                home.Points += 1;
                away.Drawn++;
                away.Points += 1;
            }
        }

        var topTeam = standings
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .FirstOrDefault();

        if (topTeam != null)
        {
            // 4. Update Tournament
            tournament.Status = "completed";
            tournament.WinnerTeamId = topTeam.TeamId;
            await _tournamentRepository.UpdateAsync(tournament);

            // 5. Log & Notify
            await _analyticsService.LogActivityByTemplateAsync(
                "TOURNAMENT_FINALIZED", // Needs addition
                new Dictionary<string, string> { { "tournamentName", tournament.Name }, { "winnerName", topTeam.TeamName } }, 
                null, 
                "نظام"
            );
            
            // Notify Admin
            await _notificationService.SendNotificationAsync(Guid.Empty, "القمة انتهت!", $"انتهت بطولة {tournament.Name} رسمياً وتوج فريق {topTeam.TeamName} باللقب!", "admin_broadcast");
            
            // Notify Winner Captain
            var winnerTeam = await _teamRepository.GetByIdAsync(topTeam.TeamId);
            if (winnerTeam != null)
            {
                await _notificationService.SendNotificationAsync(winnerTeam.CaptainId, "مبروك الفوز! 🏆", $"تهانينا! لقد فاز فريقكم {winnerTeam.Name} ببطولة {tournament.Name}.", "tournament");
            }
        }
    }
}
