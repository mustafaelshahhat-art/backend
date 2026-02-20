using Domain.Entities;
using Domain.Enums;

namespace Domain.Services;

/// <summary>
/// Pure domain service: generates match schedules for tournaments.
/// 
/// Extracted from TournamentService.CreateMatchesInternalAsync() (200+ lines)
/// to make the algorithm independently unit-testable without any DB or
/// infrastructure dependencies.
///
/// ZERO dependencies — takes tournament config + team IDs in, returns Match entities out.
/// </summary>
public static class MatchGenerator
{
    public record MatchGenerationConfig(
        Guid TournamentId,
        TournamentMode EffectiveMode,
        int NumberOfGroups,
        bool HasOpeningTeams,
        Guid? OpeningTeamAId,
        Guid? OpeningTeamBId,
        DateTime BaseDate);

    /// <summary>
    /// Generates all matches for a tournament based on the effective mode.
    /// Returns Match entities ready to be persisted (not yet saved).
    /// Also returns group assignments for registrations.
    /// </summary>
    public static MatchGenerationResult Generate(
        MatchGenerationConfig config,
        List<Guid> teamIds,
        Random? random = null)
    {
        random ??= new Random();
        var matches = new List<Match>();
        var groupAssignments = new Dictionary<Guid, int>(); // teamId → groupId

        var mode = config.EffectiveMode;

        if (mode == TournamentMode.GroupsKnockoutSingle || mode == TournamentMode.GroupsKnockoutHomeAway)
        {
            return GenerateGroupStage(config, teamIds, random);
        }
        else if (mode == TournamentMode.KnockoutSingle || mode == TournamentMode.KnockoutHomeAway)
        {
            return GenerateKnockout(config, teamIds, random);
        }
        else // League modes (RoundRobin)
        {
            return GenerateLeague(config, teamIds, random);
        }
    }

    private static MatchGenerationResult GenerateGroupStage(
        MatchGenerationConfig config, List<Guid> teamIds, Random random)
    {
        var matches = new List<Match>();
        var groupAssignments = new Dictionary<Guid, int>();

        if (config.NumberOfGroups < 1) return new(matches, groupAssignments);

        var shuffled = teamIds.OrderBy(_ => random.Next()).ToList();
        var distribution = GroupDistributionAlgorithm.Distribute(
            shuffled, config.NumberOfGroups,
            config.HasOpeningTeams ? config.OpeningTeamAId : null,
            config.HasOpeningTeams ? config.OpeningTeamBId : null);

        var validation = GroupDistributionAlgorithm.Validate(distribution, shuffled, config.NumberOfGroups);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Group distribution failed: {string.Join("; ", validation.Errors)}");

        // Record group assignments
        foreach (var (groupId, groupTeamIds) in distribution)
            foreach (var tid in groupTeamIds)
                groupAssignments[tid] = groupId;

        var groups = Enumerable.Range(1, distribution.Count).Select(g => distribution[g]).ToList();
        int dayOffset = 0;
        bool isHomeAway = config.EffectiveMode == TournamentMode.GroupsKnockoutHomeAway;

        for (int g = 0; g < groups.Count; g++)
        {
            var groupTeams = groups[g];
            bool isOpeningGroup = config.HasOpeningTeams &&
                groupTeams.Contains(config.OpeningTeamAId!.Value) &&
                groupTeams.Contains(config.OpeningTeamBId!.Value);

            var groupMatchList = new List<Match>();

            for (int i = 0; i < groupTeams.Count; i++)
            {
                for (int j = i + 1; j < groupTeams.Count; j++)
                {
                    var match = CreateMatch(config.TournamentId, groupTeams[i], groupTeams[j],
                        config.BaseDate.AddDays(dayOffset), g + 1, 1, "Group Stage");

                    if (isOpeningGroup && IsOpeningPair(config, groupTeams[i], groupTeams[j]))
                        match.IsOpeningMatch = true;

                    groupMatchList.Add(match);
                    dayOffset++;

                    if (isHomeAway)
                    {
                        groupMatchList.Add(CreateMatch(config.TournamentId, groupTeams[j], groupTeams[i],
                            config.BaseDate.AddDays(dayOffset + 2), g + 1, 1, "Group Stage"));
                        dayOffset++;
                    }
                }
            }

            // Reorder: opening match first
            if (isOpeningGroup)
                ReorderOpeningMatch(groupMatchList);

            matches.AddRange(groupMatchList);
        }

        return new(matches, groupAssignments);
    }

    private static MatchGenerationResult GenerateKnockout(
        MatchGenerationConfig config, List<Guid> teamIds, Random random)
    {
        var matches = new List<Match>();
        List<Guid> shuffled;

        if (config.HasOpeningTeams)
        {
            var a = config.OpeningTeamAId!.Value;
            var b = config.OpeningTeamBId!.Value;
            var remaining = teamIds.Where(id => id != a && id != b).OrderBy(_ => random.Next()).ToList();
            shuffled = new List<Guid> { a, b };
            shuffled.AddRange(remaining);
        }
        else
        {
            shuffled = teamIds.OrderBy(_ => random.Next()).ToList();
        }

        bool isHomeAway = config.EffectiveMode == TournamentMode.KnockoutHomeAway;
        for (int i = 0; i < shuffled.Count; i += 2)
        {
            if (i + 1 < shuffled.Count)
            {
                var match = CreateMatch(config.TournamentId, shuffled[i], shuffled[i + 1],
                    config.BaseDate.AddDays(i), null, 1, "Round 1");
                if (config.HasOpeningTeams && i == 0) match.IsOpeningMatch = true;
                matches.Add(match);

                if (isHomeAway)
                    matches.Add(CreateMatch(config.TournamentId, shuffled[i + 1], shuffled[i],
                        config.BaseDate.AddDays(i + 3), null, 1, "Round 1"));
            }
        }

        return new(matches, new Dictionary<Guid, int>());
    }

    private static MatchGenerationResult GenerateLeague(
        MatchGenerationConfig config, List<Guid> teamIds, Random random)
    {
        var matches = new List<Match>();
        List<Guid> ordered;

        if (config.HasOpeningTeams)
        {
            var a = config.OpeningTeamAId!.Value;
            var b = config.OpeningTeamBId!.Value;
            var remaining = teamIds.Where(id => id != a && id != b).OrderBy(_ => random.Next()).ToList();
            ordered = new List<Guid> { a, b };
            ordered.AddRange(remaining);
        }
        else
        {
            ordered = teamIds.OrderBy(_ => random.Next()).ToList();
        }

        bool isHomeAway = config.EffectiveMode == TournamentMode.LeagueHomeAway;
        int matchCount = 0;
        bool openingSet = false;

        for (int i = 0; i < ordered.Count; i++)
        {
            for (int j = i + 1; j < ordered.Count; j++)
            {
                var match = CreateMatch(config.TournamentId, ordered[i], ordered[j],
                    config.BaseDate.AddDays(matchCount * 2), 1, 1, "League");

                if (!openingSet && config.HasOpeningTeams && IsOpeningPair(config, ordered[i], ordered[j]))
                {
                    match.IsOpeningMatch = true;
                    openingSet = true;
                    if (matchCount > 0 && matches.Count > 0)
                    {
                        var firstDate = matches[0].Date;
                        matches[0].Date = match.Date;
                        match.Date = firstDate;
                        matches.Insert(0, match);
                    }
                    else matches.Add(match);
                }
                else matches.Add(match);

                matchCount++;
                if (isHomeAway)
                {
                    matches.Add(CreateMatch(config.TournamentId, ordered[j], ordered[i],
                        config.BaseDate.AddDays(matchCount * 2 + 1), 1, 1, "League"));
                    matchCount++;
                }
            }
        }

        return new(matches, new Dictionary<Guid, int>());
    }

    private static void ReorderOpeningMatch(List<Match> groupMatchList)
    {
        var openingMatch = groupMatchList.FirstOrDefault(m => m.IsOpeningMatch);
        if (openingMatch == null) return;

        groupMatchList.Remove(openingMatch);
        if (groupMatchList.Count > 0)
        {
            var earliestDate = groupMatchList.Min(m => m.Date);
            var openingOrigDate = openingMatch.Date;
            var firstMatch = groupMatchList.FirstOrDefault(m => m.Date == earliestDate);
            if (firstMatch != null && earliestDate < openingOrigDate)
            {
                firstMatch.Date = openingOrigDate;
                openingMatch.Date = earliestDate;
            }
        }
        groupMatchList.Insert(0, openingMatch);
    }

    private static bool IsOpeningPair(MatchGenerationConfig config, Guid t1, Guid t2)
    {
        if (!config.HasOpeningTeams) return false;
        var a = config.OpeningTeamAId!.Value;
        var b = config.OpeningTeamBId!.Value;
        return (t1 == a && t2 == b) || (t1 == b && t2 == a);
    }

    private static Match CreateMatch(Guid tournamentId, Guid home, Guid away, DateTime date, int? group, int? round, string stage)
    {
        return new Match
        {
            TournamentId = tournamentId,
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

    public record MatchGenerationResult(
        List<Match> Matches,
        Dictionary<Guid, int> GroupAssignments);

    /// <summary>
    /// Generates knockout Match entities from explicit organiser-supplied pairings.
    /// Reuses the same <see cref="CreateMatch"/> kernel as automatic generation
    /// so there is ONE match-creation path in the entire domain.
    ///
    /// This is the engine used by both:
    ///   • CreateManualKnockoutMatchesCommandHandler  (initial R1 scheduling)
    ///   • CreateManualNextRoundCommandHandler        (R2 / Semi-final rounds)
    /// </summary>
    /// <param name="tournamentId">Tournament being scheduled.</param>
    /// <param name="pairings">Organiser-supplied home/away pairs with round/stage info.</param>
    /// <param name="isHomeAway">Whether the tournament uses home-and-away legs.</param>
    /// <param name="baseDate">Start date for spreading match dates.</param>
    public static List<Match> GenerateManualKnockout(
        Guid tournamentId,
        IEnumerable<(Guid HomeTeamId, Guid AwayTeamId, int RoundNumber, string StageName)> pairings,
        bool isHomeAway,
        DateTime baseDate)
    {
        var matches = new List<Match>();
        var date = baseDate;

        foreach (var p in pairings)
        {
            matches.Add(CreateMatch(tournamentId, p.HomeTeamId, p.AwayTeamId, date, null, p.RoundNumber, p.StageName));
            date = date.AddHours(2);

            if (isHomeAway)
            {
                matches.Add(CreateMatch(tournamentId, p.AwayTeamId, p.HomeTeamId, date.AddDays(3), null, p.RoundNumber, p.StageName));
                date = date.AddHours(2);
            }
        }

        return matches;
    }
}
