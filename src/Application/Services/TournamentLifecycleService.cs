using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    public async Task CheckAndFinalizeTournamentAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null || tournament.Status == TournamentStatus.Completed) return;

        var allMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
        if (!allMatches.Any()) return;

        // Check format
        if (tournament.Format == TournamentFormat.GroupsThenKnockout || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout)
        {
            var groupMatches = allMatches.Where(m => m.GroupId != null || m.StageName == "League").ToList();
            var knockoutMatches = allMatches.Where(m => m.GroupId == null && m.StageName != "League").ToList();

            if (groupMatches.Any() && groupMatches.All(m => m.Status == MatchStatus.Finished) && !knockoutMatches.Any())
            {
                // PART 3 Logic: Transition to WaitingForOpeningMatchSelection
                {
                    tournament.ChangeStatus(TournamentStatus.WaitingForOpeningMatchSelection);
                    await _tournamentRepository.UpdateAsync(tournament, ct);
                    
                    await _analyticsService.LogActivityByTemplateAsync("GROUPS_FINISHED", new Dictionary<string, string> { { "tournamentName", tournament.Name } }, null, "System", ct);
                    await _notifier.SendTournamentUpdatedAsync(_mapper.Map<TournamentDto>(tournament));
                    
                    // Note: GenerateKnockoutR1Async should now be called by Admin action (SetOpeningMatch or similar)
                    // But for backward compatibility or auto-flow if no opening match fields are set:
                    // we could auto-advance if needed. Re-evaluating based on requirements.
                }
                return;
            }
        }

        // Check for specific stage completion (Knockout progression or Final)
        var latestRoundMatches = GetLatestRoundMatches(allMatches, ct);
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

    private List<Match> GetLatestRoundMatches(IEnumerable<Match> matches, CancellationToken ct)
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

    public async Task GenerateKnockoutR1Async(Guid tournamentId, CancellationToken ct = default)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) return;

        var allMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
        
        // 1. Calculate Standings per Group
        var registrations = await _registrationRepository.FindAsync(r => r.TournamentId == tournament.Id && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.Withdrawn), ct);
        
        var teamStats = CalculateStandings(allMatches, registrations);
        
        // 2. Qualify teams (Part 2 Logic: Smart Qualification)
        var qualificationPool = new List<TournamentStandingDto>();
        var groups = teamStats.GroupBy(s => s.GroupId ?? 0).ToList();
        var best3rdsPool = new List<TournamentStandingDto>();

        foreach (var g in groups)
        {
             var ranked = RankTeams(g.ToList());
             
             // Top 2 always qualify
             for (int i = 0; i < Math.Min(ranked.Count, 2); i++)
             {
                 qualificationPool.Add(ranked[i]);
             }

             // Others go to best-3rd pool if they are 3rd
             if (ranked.Count > 2)
             {
                 best3rdsPool.Add(ranked[2]);
             }
        }

        int baseQualifiedCount = qualificationPool.Count;
        int targetBracketSize = NextPowerOfTwo(baseQualifiedCount);

        if (baseQualifiedCount < targetBracketSize)
        {
            int needed = targetBracketSize - baseQualifiedCount;
            var ranked3rds = RankTeams(best3rdsPool);
            for (int i = 0; i < Math.Min(ranked3rds.Count, needed); i++)
            {
                qualificationPool.Add(ranked3rds[i]);
            }
        }

        // Mapping to pairing format
        var qualifiedTeams = qualificationPool.Select(t => (TeamId: t.TeamId, GroupId: t.GroupId ?? 0)).ToList();
        
        // 3. Generate Pairings
        var newMatches = new List<Match>();
        var matchDate = DateTime.UtcNow.AddDays(1);
        int round = (allMatches.Max(m => m.RoundNumber) ?? 0) + 1;
        bool isDoubleLeg = tournament.MatchType == TournamentLegType.HomeAndAway || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout;

        // PART 3 Logic: Opening Match Selection
        if (tournament.OpeningMatchHomeTeamId.HasValue && tournament.OpeningMatchAwayTeamId.HasValue)
        {
            var homeId = tournament.OpeningMatchHomeTeamId.Value;
            var awayId = tournament.OpeningMatchAwayTeamId.Value;

            // Ensure they are qualified
            if (qualifiedTeams.Any(t => t.TeamId == homeId) && qualifiedTeams.Any(t => t.TeamId == awayId))
            {
                var openingMatch = CreateMatch(tournament, homeId, awayId, matchDate, null, round, "Knockout", ct);
                openingMatch.StageName = "Opening Match";
                newMatches.Add(openingMatch);

                if (isDoubleLeg)
                    newMatches.Add(CreateMatch(tournament, awayId, homeId, matchDate.AddDays(3), null, round, "Knockout", ct));

                qualifiedTeams.RemoveAll(t => t.TeamId == homeId || t.TeamId == awayId);
                matchDate = matchDate.AddHours(2);
            }
        }

        // PART 5 Logic: Prevent same-group teams from meeting in first knockout round
        var pairings = new List<(Guid Home, Guid Away)>();
        var random = new Random();
        var pool = qualifiedTeams.OrderBy(x => random.Next()).ToList();

        while (pool.Count >= 2)
        {
            var home = pool[0];
            pool.RemoveAt(0);

            // Attempt to find someone from a different group
            var awayCandidates = pool.Where(t => t.GroupId != home.GroupId).ToList();
            var away = awayCandidates.Any() 
                ? awayCandidates[random.Next(awayCandidates.Count)] 
                : pool[0]; // Fallback to anyone if forced (e.g. all remaining from same group)
            
            pool.Remove(away);
            pairings.Add((home.TeamId, away.TeamId));
        }

        foreach(var pair in pairings)
        {
             newMatches.Add(CreateMatch(tournament, pair.Home, pair.Away, matchDate, null, round, "Knockout", ct));
             if (isDoubleLeg)
                 newMatches.Add(CreateMatch(tournament, pair.Away, pair.Home, matchDate.AddDays(3), null, round, "Knockout", ct));
             
             matchDate = matchDate.AddHours(2);
        }
        
        foreach(var m in newMatches) await _matchRepository.AddAsync(m, ct);
        
        tournament.ChangeStatus(TournamentStatus.Active);
        await _tournamentRepository.UpdateAsync(tournament, ct);

        // Notify
        await _analyticsService.LogActivityByTemplateAsync("KNOCKOUT_STARTED", new Dictionary<string, string> { { "tournamentName", tournament.Name } }, null, "System", ct);
        await _notificationService.SendNotificationAsync(Guid.Empty, "بدء الأدوار الإقصائية", $"تأهلت الفرق وبدأت الأدوار الإقصائية لبطولة {tournament.Name}", "all", ct);
        
        // Notify Real-Time
        await _notifier.SendMatchesGeneratedAsync(_mapper.Map<IEnumerable<MatchDto>>(newMatches));
    }

    private List<TournamentStandingDto> RankTeams(List<TournamentStandingDto> teams)
    {
        return teams.OrderByDescending(s => s.Points)
                    .ThenByDescending(s => s.GoalDifference)
                    .ThenByDescending(s => s.GoalsFor)
                    // Part 2 Logic: Red Cards ASC, Yellow Cards ASC
                    .ThenBy(s => s.RedCards)
                    .ThenBy(s => s.YellowCards)
                    .ThenBy(x => Guid.NewGuid()) // Random draw
                    .ToList();
    }

    private int NextPowerOfTwo(int n)
    {
        if (n <= 2) return 2;
        if (n <= 4) return 4;
        if (n <= 8) return 8;
        if (n <= 16) return 16;
        if (n <= 32) return 32;
        return 64; // reasonable upper limit for tournaments
    }

    private async Task GenerateNextKnockoutRoundAsync(Tournament tournament, List<Match> previousRoundMatches, CancellationToken ct = default)
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
                  newMatches.Add(CreateMatch(tournament, winners[i], winners[i+1], matchDate, null, round, stage, ct));
                  
                  if (isDoubleLeg && stage != "Final")
                  {
                       newMatches.Add(CreateMatch(tournament, winners[i+1], winners[i], matchDate.AddDays(3), null, round, stage, ct));
                  }
             }
        }
        
        foreach(var m in newMatches) await _matchRepository.AddAsync(m, ct);
        
        // Notify Real-Time
        await _notifier.SendMatchesGeneratedAsync(_mapper.Map<IEnumerable<MatchDto>>(newMatches));
    }

    private async Task FinalizeTournamentAsync(Tournament tournament, List<Match> finalMatches, IEnumerable<Match> allMatches, CancellationToken ct = default)
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
         
         tournament.ChangeStatus(TournamentStatus.Completed);
         tournament.WinnerTeamId = winnerId;
         await _tournamentRepository.UpdateAsync(tournament, ct);
         
         var winnerTeam = await _teamRepository.GetByIdAsync(winnerId, ct);
         
         await _analyticsService.LogActivityByTemplateAsync("TOURNAMENT_FINALIZED", new Dictionary<string, string> { { "tournamentName", tournament.Name }, { "winnerName", winnerTeam?.Name ?? "Unknown" } }, null, "نظام", ct);
         
         await _notificationService.SendNotificationAsync(Guid.Empty, "القمة انتهت!", $"انتهت بطولة {tournament.Name} رسمياً وتوج فريق {winnerTeam?.Name} باللقب!", "admin_broadcast", ct);
         
         await _notifier.SendTournamentUpdatedAsync(_mapper.Map<TournamentDto>(tournament));
    }

    public List<TournamentStandingDto> CalculateStandings(IEnumerable<Match> allMatches, IEnumerable<TeamRegistration> teams)
    {
        var standings = teams.Select(t => new TournamentStandingDto 
        { 
            TeamId = t.TeamId, 
            TeamName = t.Team?.Name ?? "فريق",
            TeamLogoUrl = t.Team?.Logo,
            GroupId = GetGroupId(allMatches, t.TeamId) 
        }).ToList();
        
        foreach(var m in allMatches.Where(mm => (mm.GroupId != null || mm.StageName == "League") && mm.Status == MatchStatus.Finished))
        {
             var h = standings.FirstOrDefault(s => s.TeamId == m.HomeTeamId);
             var a = standings.FirstOrDefault(s => s.TeamId == m.AwayTeamId);
             
             if (h != null) 
             { 
                 h.Played++;
                 h.GoalsFor += m.HomeScore;
                 h.GoalsAgainst += m.AwayScore;
                 
                 if (m.HomeScore > m.AwayScore) { h.Points += 3; h.Won++; h.Form.Add("W"); }
                 else if (m.HomeScore == m.AwayScore) { h.Points += 1; h.Drawn++; h.Form.Add("D"); }
                 else { h.Lost++; h.Form.Add("L"); }
             }
             
             if (a != null) 
             { 
                 a.Played++;
                 a.GoalsFor += m.AwayScore;
                 a.GoalsAgainst += m.HomeScore;

                 if (m.AwayScore > m.HomeScore) { a.Points += 3; a.Won++; a.Form.Add("W"); }
                 else if (m.AwayScore == m.HomeScore) { a.Points += 1; a.Drawn++; a.Form.Add("D"); }
                 else { a.Lost++; a.Form.Add("L"); }
             }

             // Count Cards
             if (m.Events != null)
             {
                 foreach(var e in m.Events)
                 {
                     var teamStanding = standings.FirstOrDefault(s => s.TeamId == e.TeamId);
                     if (teamStanding == null) continue;
                     
                     if (e.Type == MatchEventType.YellowCard) teamStanding.YellowCards++;
                     else if (e.Type == MatchEventType.RedCard) teamStanding.RedCards++;
                 }
             }
        }
        
        // Final Sort: Group > Points > GD > GF > (Lower) RedCards > (Lower) YellowCards
        return standings
            .OrderBy(s => s.GroupId ?? 0)
            .ThenByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ThenBy(s => s.RedCards)
            .ThenBy(s => s.YellowCards)
            .ToList();
    }
    
    private int? GetGroupId(IEnumerable<Match> matches, Guid teamId)
    {
        var match = matches.FirstOrDefault(m => m.HomeTeamId == teamId || m.AwayTeamId == teamId);
        if (match == null) return null;
        if (match.GroupId != null) return match.GroupId;
        if (match.StageName == "League") return 1;
        return null;
    }

    private Match CreateMatch(Tournament t, Guid home, Guid away, DateTime date, int? group, int? round, string stage, CancellationToken ct)
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
