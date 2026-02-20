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
using Domain.Services;
using AutoMapper; // Imported

namespace Application.Services;

public class TournamentLifecycleService : ITournamentLifecycleService
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRealTimeNotifier _notifier;
    private readonly IMapper _mapper;

    public TournamentLifecycleService(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRealTimeNotifier notifier,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _registrationRepository = registrationRepository;
        _notifier = notifier;
        _mapper = mapper;
    }

    public async Task<TournamentLifecycleResult> CheckAndFinalizeTournamentAsync(Guid tournamentId, CancellationToken ct = default)
    {
        var result = new TournamentLifecycleResult { TournamentId = tournamentId };

        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null || tournament.Status == TournamentStatus.Completed) return result;

        result.TournamentName = tournament.Name;
        result.CreatorUserId = tournament.CreatorUserId;

        var allMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);
        if (!allMatches.Any()) return result;

        // Check format
        if (tournament.Format == TournamentFormat.GroupsThenKnockout || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout)
        {
            var groupMatches = allMatches.Where(m => m.GroupId != null || m.StageName == "League").ToList();
            var knockoutMatches = allMatches.Where(m => m.GroupId == null && m.StageName != "League").ToList();

            if (groupMatches.Any() && groupMatches.All(m => m.Status == MatchStatus.Finished) && !knockoutMatches.Any())
            {
                // ── Manual Qualification Policy ──
                // Instead of auto-qualifying teams, transition to ManualQualificationPending
                // so the organiser can select advancers.  Generation is BLOCKED until confirmed.
                if (tournament.SchedulingMode == SchedulingMode.Manual)
                {
                    tournament.ChangeStatus(TournamentStatus.ManualQualificationPending);
                    await _tournamentRepository.UpdateAsync(tournament, ct);
                    result.GroupsFinished = true;
                    result.ManualQualificationRequired = true;
                    return result;
                }

                // Automatic mode: existing behaviour unchanged
                var knockoutResult = await GenerateKnockoutR1Async(tournament.Id, ct);
                knockoutResult.GroupsFinished = true;
                return knockoutResult;
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
                 return await FinalizeTournamentAsync(tournament, latestRoundMatches, allMatches, ct);
            }
            else
            {
                 // Generate Next Round
                 return await GenerateNextKnockoutRoundAsync(tournament, latestRoundMatches, ct);
            }
        }

        return result;
    }

    private List<Match> GetLatestRoundMatches(IEnumerable<Match> matches, CancellationToken ct)
    {
        // Only consider knockout matches (GroupId == null) that are not league matches.
        // Group-stage matches share the same RoundNumber (e.g. both round 1) and must
        // not be mixed in here — otherwise Count will be wrong and finalization won't trigger.
        var actionableMatches = matches
            .Where(m => m.GroupId == null && m.StageName != "League")
            .ToList();

        if (!actionableMatches.Any()) return new List<Match>();

        var maxRound = actionableMatches.Max(m => m.RoundNumber);
        if (!maxRound.HasValue)
            return actionableMatches; // No round numbers assigned — treat them all as the latest

        return actionableMatches.Where(m => m.RoundNumber == maxRound).ToList();
    }

    public async Task<TournamentLifecycleResult> GenerateKnockoutR1Async(Guid tournamentId, CancellationToken ct = default)
    {
        var result = new TournamentLifecycleResult { TournamentId = tournamentId };
        var tournament = await _tournamentRepository.GetByIdAsync(tournamentId, ct);
        if (tournament == null) return result;

        result.TournamentName = tournament.Name;
        result.CreatorUserId = tournament.CreatorUserId;

        // ── Manual Qualification Gate ──
        // If the organiser has not yet confirmed qualified teams, block generation entirely.
        // This is a belt-and-suspenders guard — the primary gate is in CheckAndFinalizeTournamentAsync.
        if (tournament.RequiresManualQualification())
        {
            result.ManualQualificationRequired = true;
            return result;
        }

        var allMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId, ct);

        // ── Qualification path: Manual (pre-confirmed) vs Automatic (standings-derived) ──
        List<(Guid TeamId, int GroupId)> qualifiedTeams;

        if (tournament.Status == TournamentStatus.QualificationConfirmed)
        {
            // Manual path: the organiser already selected teams via ConfirmManualQualificationCommand.
            // IsQualifiedForKnockout was set explicitly — no standings math needed here.
            var confirmedRegs = await _registrationRepository.FindAsync(
                r => r.TournamentId == tournamentId && r.IsQualifiedForKnockout, ct);
            qualifiedTeams = confirmedRegs
                .Select(r => (TeamId: r.TeamId, GroupId: r.GroupId ?? 0))
                .ToList();
        }
        else
        {
            // Automatic path: derive qualified teams from standings (existing logic, unchanged).
            var registrations = await _registrationRepository.FindAsync(
                r => r.TournamentId == tournament.Id
                     && (r.Status == RegistrationStatus.Approved || r.Status == RegistrationStatus.Withdrawn), ct);

            var teamStats = StandingsCalculator.Calculate(allMatches, registrations);

            var qualificationPool = new List<StandingsCalculator.TeamStanding>();
            var groups = teamStats.GroupBy(s => s.GroupId ?? 0).ToList();
            var best3rdsPool = new List<StandingsCalculator.TeamStanding>();

            foreach (var g in groups)
            {
                var ranked = StandingsCalculator.Rank(g.ToList());
                var groupSize = ranked.Count;
                int teamsToQualify = groupSize <= 2 ? 1 : 2;

                for (int i = 0; i < Math.Min(ranked.Count, teamsToQualify); i++)
                    qualificationPool.Add(ranked[i]);

                if (ranked.Count > 2)
                    best3rdsPool.Add(ranked[2]);
            }

            int baseQualifiedCount = qualificationPool.Count;
            int targetBracketSize = NextPowerOfTwo(baseQualifiedCount);

            if (baseQualifiedCount < targetBracketSize)
            {
                int needed = targetBracketSize - baseQualifiedCount;
                var ranked3rds = StandingsCalculator.Rank(best3rdsPool);
                for (int i = 0; i < Math.Min(ranked3rds.Count, needed); i++)
                    qualificationPool.Add(ranked3rds[i]);
            }

            qualifiedTeams = qualificationPool
                .Select(t => (TeamId: t.TeamId, GroupId: t.GroupId ?? 0))
                .ToList();
        }

        // 3. Determine round number & manual-draw gate
        //    Groups→Knockout R1 is NEVER a final (finals come much later).
        var newMatches = new List<Match>();
        var matchDate = DateTime.UtcNow.AddDays(1);
        int round = (allMatches.Max(m => m.RoundNumber) ?? 0) + 1;
        bool isDoubleLeg = tournament.MatchType == TournamentLegType.HomeAndAway || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout;

        // ── Manual Draw Policy ──
        if (tournament.RequiresManualDraw(round, isFinalRound: false))
        {
            result.ManualDrawRequired = true;
            result.ManualDrawRoundNumber = round;
            return result;
        }

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
        
        // PERF-FIX: Batch insert all matches in single roundtrip
        await _matchRepository.AddRangeAsync(newMatches, ct);
        
        tournament.ChangeStatus(TournamentStatus.Active);
        await _tournamentRepository.UpdateAsync(tournament, ct);

        // Notify Real-Time
        await _notifier.SendMatchesGeneratedAsync(_mapper.Map<IEnumerable<MatchDto>>(newMatches));

        result.NextRoundGenerated = true;
        result.MatchesGenerated = newMatches.Count;
        result.RoundNumber = round;
        return result;
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

    private async Task<TournamentLifecycleResult> GenerateNextKnockoutRoundAsync(Tournament tournament, List<Match> previousRoundMatches, CancellationToken ct = default)
    {
        var result = new TournamentLifecycleResult
        {
            TournamentId = tournament.Id,
            TournamentName = tournament.Name,
            CreatorUserId = tournament.CreatorUserId
        };

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
        
        if (winners.Count < 2) return result;

        var newMatches = new List<Match>();
        var matchDate = DateTime.UtcNow.AddDays(3);
        int round = (previousRoundMatches.First().RoundNumber ?? 0) + 1;
        bool isDoubleLeg = tournament.MatchType == TournamentLegType.HomeAndAway || tournament.Format == TournamentFormat.GroupsWithHomeAwayKnockout;

        // ── Manual Draw Policy ──
        // winners.Count == 2 means the next match is the Final → always auto-generated.
        bool isFinalRound = winners.Count == 2;
        if (tournament.RequiresManualDraw(round, isFinalRound))
        {
            result.ManualDrawRequired = true;
            result.ManualDrawRoundNumber = round;
            return result;
        }

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
        
        // PERF-FIX: Batch insert all matches in single roundtrip
        await _matchRepository.AddRangeAsync(newMatches, ct);
        
        // Notify Real-Time
        await _notifier.SendMatchesGeneratedAsync(_mapper.Map<IEnumerable<MatchDto>>(newMatches));

        result.NextRoundGenerated = true;
        result.MatchesGenerated = newMatches.Count;
        result.RoundNumber = round;
        return result;
    }

    private async Task<TournamentLifecycleResult> FinalizeTournamentAsync(Tournament tournament, List<Match> finalMatches, IEnumerable<Match> allMatches, CancellationToken ct = default)
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

         // Resolve winner team name from final match navigation
         var refMatch = await _matchRepository.GetByIdAsync(finalMatches[0].Id,
             new System.Linq.Expressions.Expression<Func<Match, object>>[] { m => m.HomeTeam!, m => m.AwayTeam! }, ct);
         var winnerTeamName = refMatch != null
             ? (winnerId == refMatch.HomeTeamId ? refMatch.HomeTeam?.Name : refMatch.AwayTeam?.Name)
             : null;

         await _notifier.SendTournamentUpdatedAsync(_mapper.Map<TournamentDto>(tournament));

         return new TournamentLifecycleResult
         {
             TournamentId = tournament.Id,
             TournamentName = tournament.Name,
             CreatorUserId = tournament.CreatorUserId,
             TournamentFinalized = true,
             WinnerTeamId = winnerId,
             WinnerTeamName = winnerTeamName
         };
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
