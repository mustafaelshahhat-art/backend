using Domain.Entities;
using Domain.Enums;

namespace Domain.Services;

/// <summary>
/// Pure domain service: builds knockout brackets from group stage results.
/// 
/// Extracted from TournamentLifecycleService.GenerateKnockoutR1Async() (150+ lines)
/// to make qualification logic and bracket pairing independently testable.
///
/// ZERO dependencies — takes standings + config in, returns pairings out.
/// </summary>
public static class KnockoutBracketBuilder
{
    public record QualifiedTeam(Guid TeamId, int GroupId, int GroupRank);

    public record KnockoutPairing(Guid HomeTeamId, Guid AwayTeamId);

    public record BracketResult(
        List<KnockoutPairing> Pairings,
        KnockoutPairing? OpeningPairing);

    /// <summary>
    /// Determines which teams qualify from group stage standings.
    /// 
    /// Rules:
    /// - Groups with ≤ 2 teams: top 1 qualifies
    /// - Groups with > 2 teams: top 2 qualify
    /// - Remaining slots filled by best 3rd-place teams across groups
    /// - Total qualified count rounded up to next power of 2
    /// </summary>
    public static List<QualifiedTeam> DetermineQualifiedTeams(
        List<StandingsCalculator.TeamStanding> standings)
    {
        var qualificationPool = new List<QualifiedTeam>();
        var best3rdsPool = new List<StandingsCalculator.TeamStanding>();

        var groups = standings.GroupBy(s => s.GroupId ?? 0).ToList();

        foreach (var group in groups)
        {
            var ranked = StandingsCalculator.Rank(group.ToList());
            int teamsToQualify = ranked.Count <= 2 ? 1 : 2;

            for (int i = 0; i < Math.Min(ranked.Count, teamsToQualify); i++)
            {
                qualificationPool.Add(new QualifiedTeam(
                    ranked[i].TeamId,
                    ranked[i].GroupId ?? 0,
                    i + 1));
            }

            if (ranked.Count > 2)
                best3rdsPool.Add(ranked[2]);
        }

        int targetSize = NextPowerOfTwo(qualificationPool.Count);
        if (qualificationPool.Count < targetSize)
        {
            int needed = targetSize - qualificationPool.Count;
            var ranked3rds = StandingsCalculator.Rank(best3rdsPool);
            for (int i = 0; i < Math.Min(ranked3rds.Count, needed); i++)
            {
                qualificationPool.Add(new QualifiedTeam(
                    ranked3rds[i].TeamId,
                    ranked3rds[i].GroupId ?? 0,
                    3));
            }
        }

        return qualificationPool;
    }

    /// <summary>
    /// Creates pairings for knockout round 1, ensuring teams from
    /// the same group don't meet when possible.
    /// </summary>
    public static BracketResult CreatePairings(
        List<QualifiedTeam> qualifiedTeams,
        Guid? openingHomeTeamId = null,
        Guid? openingAwayTeamId = null,
        Random? random = null)
    {
        random ??= new Random();
        KnockoutPairing? openingPairing = null;

        var pool = qualifiedTeams.ToList();

        // Handle opening match first
        if (openingHomeTeamId.HasValue && openingAwayTeamId.HasValue)
        {
            var homeExists = pool.Any(t => t.TeamId == openingHomeTeamId.Value);
            var awayExists = pool.Any(t => t.TeamId == openingAwayTeamId.Value);

            if (homeExists && awayExists)
            {
                openingPairing = new KnockoutPairing(openingHomeTeamId.Value, openingAwayTeamId.Value);
                pool.RemoveAll(t => t.TeamId == openingHomeTeamId.Value || t.TeamId == openingAwayTeamId.Value);
            }
        }

        // Prevent same-group teams from meeting in first round
        var pairings = new List<KnockoutPairing>();
        var shuffled = pool.OrderBy(_ => random.Next()).ToList();

        while (shuffled.Count >= 2)
        {
            var home = shuffled[0];
            shuffled.RemoveAt(0);

            var crossGroupCandidates = shuffled.Where(t => t.GroupId != home.GroupId).ToList();
            var away = crossGroupCandidates.Any()
                ? crossGroupCandidates[random.Next(crossGroupCandidates.Count)]
                : shuffled[0]; // fallback if forced

            shuffled.Remove(away);
            pairings.Add(new KnockoutPairing(home.TeamId, away.TeamId));
        }

        return new BracketResult(pairings, openingPairing);
    }

    /// <summary>
    /// Determines winners from a set of completed knockout matches.
    /// Handles both single-leg and double-leg (home & away) formats.
    /// </summary>
    public static List<Guid> DetermineWinners(List<Match> roundMatches)
    {
        var winners = new List<Guid>();
        var processed = new HashSet<Guid>();

        foreach (var match in roundMatches)
        {
            if (processed.Contains(match.Id)) continue;

            // Check for return leg
            var returnLeg = roundMatches.FirstOrDefault(m =>
                m.Id != match.Id &&
                m.HomeTeamId == match.AwayTeamId &&
                m.AwayTeamId == match.HomeTeamId);

            Guid winnerId;
            if (returnLeg != null)
            {
                processed.Add(returnLeg.Id);
                int agg1 = match.HomeScore + returnLeg.AwayScore;
                int agg2 = match.AwayScore + returnLeg.HomeScore;
                winnerId = agg1 >= agg2 ? match.HomeTeamId : match.AwayTeamId; // Home team advantage on tie
            }
            else
            {
                winnerId = match.HomeScore >= match.AwayScore ? match.HomeTeamId : match.AwayTeamId;
            }

            winners.Add(winnerId);
        }

        return winners;
    }

    public static int NextPowerOfTwo(int n)
    {
        if (n <= 2) return 2;
        if (n <= 4) return 4;
        if (n <= 8) return 8;
        if (n <= 16) return 16;
        if (n <= 32) return 32;
        return 64;
    }
}
