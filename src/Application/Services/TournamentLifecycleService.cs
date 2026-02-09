using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches; // Imported
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using AutoMapper; // Imported

namespace Application.Services;

public class TournamentLifecycleService : ITournamentLifecycleService
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly INotificationService _notificationService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IRealTimeNotifier _notifier;
    private readonly IMapper _mapper;

    public TournamentLifecycleService(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Team> teamRepository,
        INotificationService notificationService,
        IAnalyticsService analyticsService,
        IRealTimeNotifier notifier,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _registrationRepository = registrationRepository;
        _teamRepository = teamRepository;
        _notificationService = notificationService;
        _analyticsService = analyticsService;
        _notifier = notifier;
        _mapper = mapper;
    }

    public async Task CheckAndFinalizeTournamentAsync(Guid tournamentId)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
        if (tournament == null || tournament.Status == "completed") return;

        var allMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        if (!allMatches.Any()) return;

        // Check format
        if (tournament.Format == TournamentFormat.GroupsThenKnockout || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout)
        {
            var groupMatches = allMatches.Where(m => m.GroupId != null || m.StageName == "League").ToList();
            var knockoutMatches = allMatches.Where(m => m.GroupId == null && m.StageName != "League").ToList();

            if (groupMatches.Any() && groupMatches.All(m => m.Status == MatchStatus.Finished) && !knockoutMatches.Any())
            {
                // Advance to Knockout R1
                await GenerateKnockoutR1Async(tournament, allMatches);
                return;
            }
        }

        // Check for specific stage completion (Knockout progression or Final)
        var latestRoundMatches = GetLatestRoundMatches(allMatches);
        if (latestRoundMatches.Any() && latestRoundMatches.First().StageName != "League" && latestRoundMatches.All(m => m.Status == MatchStatus.Finished))
        {
            // If it was the final, complete tournament
            bool isKnockoutDoubleLeg = tournament.MatchType == TournamentLegType.HomeAndAway || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout;
            if (latestRoundMatches.Count == 1 || (isKnockoutDoubleLeg && latestRoundMatches.Count == 2 && latestRoundMatches.First().StageName == "Final"))
            {
                 await FinalizeTournamentAsync(tournament, latestRoundMatches, allMatches);
            }
            else
            {
                 // Generate Next Round
                 await GenerateNextKnockoutRoundAsync(tournament, latestRoundMatches);
            }
        }
    }

    private List<Match> GetLatestRoundMatches(IEnumerable<Match> matches)
    {
        var actionableMatches = matches.Where(m => m.StageName != "League").ToList();
        if (!actionableMatches.Any()) return new List<Match>();

        var maxRound = actionableMatches.Max(m => m.RoundNumber);
        if (!maxRound.HasValue) 
        {
             // Maybe groups?
             if (matches.Any(m => m.GroupId != null)) return new List<Match>(); // Ignoring group matches for round logic here
             return matches.ToList(); 
        }
        return actionableMatches.Where(m => m.RoundNumber == maxRound).ToList();
    }

    private async Task GenerateKnockoutR1Async(Tournament tournament, IEnumerable<Match> allMatches)
    {
        // ... (existing logic for seeding/pairing same as before) ...
        // 1. Calculate Standings per Group
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournament.Id && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.Withdrawn));
        
        var teamStats = CalculateStandings(allMatches, registrations);
        
        // 2. Qualify teams
        var qualifiedTeams = new List<(Guid TeamId, int GroupId, int Rank)>();
        
        var groups = teamStats.GroupBy(s => s.GroupId ?? 0);
        foreach (var g in groups)
        {
             var ranked = g.OrderByDescending(s => s.Points)
                           .ThenByDescending(s => s.GoalDifference)
                           .ThenByDescending(s => s.GoalsFor)
                           .ToList();
             for (int i = 0; i < Math.Min(ranked.Count, tournament.QualifiedTeamsPerGroup); i++)
             {
                 qualifiedTeams.Add((ranked[i].TeamId, g.Key, i + 1));
             }
        }
        
        // 3. Generate Pairings
        var newMatches = new List<Match>();
        var matchDate = DateTime.UtcNow.AddDays(1);
        int round = (allMatches.Max(m => m.RoundNumber) ?? 0) + 1;
        bool isDoubleLeg = tournament.MatchType == TournamentLegType.HomeAndAway || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout;

        // Strategy: Separate Rank 1 and Rank 2
        var pot1 = qualifiedTeams.Where(t => t.Rank == 1).ToList();
        var pot2 = qualifiedTeams.Where(t => t.Rank == 2).ToList();
        var others = qualifiedTeams.Where(t => t.Rank > 2).ToList(); // If any
        
        var pairings = new List<(Guid Home, Guid Away)>();

        bool useRankBased = tournament.SeedingMode == SeedingMode.RankBased;
        if (tournament.SeedingMode == SeedingMode.ShuffleOnly) useRankBased = false;
        
        // If exact Winners vs Runners logic applies (Even groups, 2 qualified) AND RankBased enabled
        if (useRankBased && pot1.Count > 0 && pot1.Count == pot2.Count && others.Count == 0)
        {
             bool success = false;
             var random = new Random();
             
             // Max 100 retries
             for(int attempt=0; attempt<100; attempt++)
             {
                 var currentPairings = new List<(Guid Home, Guid Away)>();
                 var availableRunners = pot2.OrderBy(x => random.Next()).ToList();
                 bool validAttempt = true;
                 
                 foreach(var winner in pot1)
                 {
                     var validRunner = availableRunners.FirstOrDefault(r => r.GroupId != winner.GroupId);
                     if (validRunner.TeamId == Guid.Empty)
                     {
                         validAttempt = false;
                         break;
                     }
                     currentPairings.Add((winner.TeamId, validRunner.TeamId));
                     availableRunners.Remove(validRunner);
                 }
                 
                 if (validAttempt)
                 {
                     pairings = currentPairings;
                     success = true;
                     break;
                 }
             }
             
             if (!success)
             {
                 var all = pot1.Concat(pot2).Select(t => t.TeamId).OrderBy(x => random.Next()).ToList();
                 for(int i=0; i<all.Count; i+=2) pairings.Add((all[i], all[i+1]));
             }
        }
        else
        {
            var all = qualifiedTeams.OrderBy(x => x.Rank).ThenBy(x => Guid.NewGuid()).ToList();
            var pool = all.Select(t => t).ToList();
            while(pool.Count >= 2)
            {
                var home = pool[0]; pool.RemoveAt(0);
                var away = pool.FirstOrDefault(t => t.GroupId != home.GroupId);
                if (away.TeamId == Guid.Empty) away = pool[0];
                pool.Remove(away);
                pairings.Add((home.TeamId, away.TeamId));
            }
        }

        foreach(var pair in pairings)
        {
             newMatches.Add(CreateMatch(tournament, pair.Home, pair.Away, matchDate, null, round, "Knockout"));
             if (isDoubleLeg)
                 newMatches.Add(CreateMatch(tournament, pair.Away, pair.Home, matchDate.AddDays(3), null, round, "Knockout"));
             
             matchDate = matchDate.AddHours(2);
        }
        
        foreach(var m in newMatches) await _matchRepository.AddAsync(m);
        
        // Notify
        await _analyticsService.LogActivityByTemplateAsync("KNOCKOUT_STARTED", new Dictionary<string, string> { { "tournamentName", tournament.Name } }, null, "System");
        await _notificationService.SendNotificationAsync(Guid.Empty, "بدء الأدوار الإقصائية", $"تأهلت الفرق وبدأت الأدوار الإقصائية لبطولة {tournament.Name}", "all");
        
        // Notify Real-Time
        await _notifier.SendMatchesGeneratedAsync(_mapper.Map<IEnumerable<MatchDto>>(newMatches));
    }

    private async Task GenerateNextKnockoutRoundAsync(Tournament tournament, List<Match> previousRoundMatches)
    {
        // Identify winners
        var winners = new List<Guid>();
        var processedMatches = new HashSet<Guid>();
        
        foreach(var match in previousRoundMatches)
        {
             if (processedMatches.Contains(match.Id)) continue;
             
             Guid winnerId = Guid.Empty;
             var returnLeg = previousRoundMatches.FirstOrDefault(m => m.Id != match.Id && m.HomeTeamId == match.AwayTeamId && m.AwayTeamId == match.HomeTeamId);
             
             if (returnLeg != null)
             {
                 processedMatches.Add(returnLeg.Id);
                 int score1 = match.HomeScore + returnLeg.AwayScore;
                 int score2 = match.AwayScore + returnLeg.HomeScore;
                 
                 if (score1 > score2) winnerId = match.HomeTeamId;
                 else if (score2 > score1) winnerId = match.AwayTeamId;
                 else winnerId = match.HomeTeamId; // Default
             }
             else
             {
                 winnerId = match.HomeScore > match.AwayScore ? match.HomeTeamId : match.AwayTeamId;
                 if (match.HomeScore == match.AwayScore) winnerId = match.HomeTeamId;
             }
             
             winners.Add(winnerId);
        }
        
        if (winners.Count < 2) return;
        
        var newMatches = new List<Match>();
        var matchDate = DateTime.UtcNow.AddDays(3);
        int round = (previousRoundMatches.First().RoundNumber ?? 0) + 1;
        bool isDoubleLeg = tournament.MatchType == TournamentLegType.HomeAndAway || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout;
        
        for(int i = 0; i < winners.Count; i+=2)
        {
             if (i+1 < winners.Count)
             {
                  string stage = winners.Count == 2 ? "Final" : "Knockout";
                  newMatches.Add(CreateMatch(tournament, winners[i], winners[i+1], matchDate, null, round, stage));
                  
                  if (isDoubleLeg && stage != "Final")
                  {
                       newMatches.Add(CreateMatch(tournament, winners[i+1], winners[i], matchDate.AddDays(3), null, round, stage));
                  }
             }
        }
        
        foreach(var m in newMatches) await _matchRepository.AddAsync(m);
        
        // Notify Real-Time
        await _notifier.SendMatchesGeneratedAsync(_mapper.Map<IEnumerable<MatchDto>>(newMatches));
    }

    private async Task FinalizeTournamentAsync(Tournament tournament, List<Match> finalMatches, IEnumerable<Match> allMatches)
    {
         // Determine winner
         Guid winnerId;
         if (finalMatches.Count > 1)
         {
             // Double Leg Final logic
             var m1 = finalMatches[0];
             var m2 = finalMatches[1];
             int s1 = m1.HomeScore + m2.AwayScore;
             int s2 = m1.AwayScore + m2.HomeScore;
             winnerId = s1 > s2 ? m1.HomeTeamId : m1.AwayTeamId; 
         } 
         else 
         {
             var finalMatch = finalMatches.First();
             winnerId = finalMatch.HomeScore > finalMatch.AwayScore ? finalMatch.HomeTeamId : finalMatch.AwayTeamId;
         }
         
         tournament.Status = "completed";
         tournament.WinnerTeamId = winnerId;
         await _tournamentRepository.UpdateAsync(tournament);
         
         var winnerTeam = await _teamRepository.GetByIdAsync(winnerId);
         
         await _analyticsService.LogActivityByTemplateAsync("TOURNAMENT_FINALIZED", new Dictionary<string, string> { { "tournamentName", tournament.Name }, { "winnerName", winnerTeam?.Name ?? "Unknown" } }, null, "نظام");
         
         await _notificationService.SendNotificationAsync(Guid.Empty, "القمة انتهت!", $"انتهت بطولة {tournament.Name} رسمياً وتوج فريق {winnerTeam?.Name} باللقب!", "admin_broadcast");
         
         await _notifier.SendTournamentUpdatedAsync(_mapper.Map<TournamentDto>(tournament));
    }

    private List<TournamentStandingDto> CalculateStandings(IEnumerable<Match> allMatches, IEnumerable<TeamRegistration> teams)
    {
        // Reuse logic from TournamentService (duplicated here for isolation or inject)
        // Simplified
        var standings = teams.Select(t => new TournamentStandingDto { TeamId = t.TeamId, GroupId = GetGroupId(allMatches, t.TeamId) }).ToList();
        // ... (calc stats for group matches only)
        foreach(var m in allMatches.Where(mm => (mm.GroupId != null || mm.StageName == "League") && mm.Status == MatchStatus.Finished))
        {
             var h = standings.FirstOrDefault(s => s.TeamId == m.HomeTeamId);
             var a = standings.FirstOrDefault(s => s.TeamId == m.AwayTeamId);
             
             if (h != null) 
             { 
                 h.Points += (m.HomeScore > m.AwayScore ? 3 : (m.HomeScore == m.AwayScore ? 1 : 0)); 
                 h.GoalsFor += m.HomeScore;
                 h.GoalsAgainst += m.AwayScore;
             }
             
             if (a != null) 
             { 
                 a.Points += (m.AwayScore > m.HomeScore ? 3 : (m.AwayScore == m.HomeScore ? 1 : 0)); 
                 a.GoalsFor += m.AwayScore;
                 a.GoalsAgainst += m.HomeScore;
             }
        }
        return standings;
    }
    
    private int? GetGroupId(IEnumerable<Match> matches, Guid teamId)
    {
        var match = matches.FirstOrDefault(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId);
        if (match == null) return null;
        if (match.GroupId != null) return match.GroupId;
        if (match.StageName == "League") return 1;
        return null;
    }

    private Match CreateMatch(Tournament t, Guid home, Guid away, DateTime date, int? group, int? round, string stage)
    {
        return new Match
        {
            TournamentId = t.Id,
            HomeTeamId = home,
            AwayTeamId = away,
            Status = MatchStatus.Scheduled,
            Date = date,
            GroupId = group,
            RoundNumber = round,
            StageName = stage,
            HomeScore = 0,
            AwayScore = 0
        };
    }
}
