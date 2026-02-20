using Domain.Entities;
using Domain.Enums;

namespace Domain.Services;

/// <summary>
/// Pure domain service: calculates tournament standings from match results.
/// 
/// Extracted from TournamentLifecycleService.CalculateStandings() and
/// TournamentService.GetStandingsAsync() to eliminate duplication and
/// make the algorithm independently unit-testable.
///
/// ZERO dependencies — takes data in, returns data out.
/// </summary>
public static class StandingsCalculator
{
    public record TeamStanding(
        Guid TeamId,
        string TeamName,
        int? GroupId,
        int Played,
        int Won,
        int Drawn,
        int Lost,
        int GoalsFor,
        int GoalsAgainst,
        int GoalDifference,
        int Points,
        int YellowCards,
        int RedCards,
        int Rank,
        List<string> Form);

    /// <summary>
    /// Calculates standings for all teams in a tournament.
    /// 
    /// Ranking criteria (in order):
    /// 1. Points (DESC)
    /// 2. Goal Difference (DESC)
    /// 3. Goals For (DESC)
    /// 4. Red Cards (ASC — fewer is better)
    /// 5. Yellow Cards (ASC — fewer is better)
    /// 
    /// Processes only group-stage/league matches (GroupId != null OR StageName == "League"/"Group Stage").
    /// </summary>
    public static List<TeamStanding> Calculate(
        IEnumerable<Match> allMatches,
        IEnumerable<TeamRegistration> teams)
    {
        // Build mutable accumulators
        var accumulators = teams.ToDictionary(
            t => t.TeamId,
            t => new StandingAccumulator
            {
                TeamId = t.TeamId,
                TeamName = t.Team?.Name ?? "فريق",
                GroupId = t.GroupId
            });

        // Only consider finished group/league matches
        var relevantMatches = allMatches.Where(m =>
            (m.GroupId != null || m.StageName == "League" || m.StageName == "Group Stage") &&
            m.Status == MatchStatus.Finished);

        foreach (var match in relevantMatches)
        {
            if (accumulators.TryGetValue(match.HomeTeamId, out var home))
            {
                home.Played++;
                home.GoalsFor += match.HomeScore;
                home.GoalsAgainst += match.AwayScore;

                if (match.HomeScore > match.AwayScore)
                {
                    home.Points += 3;
                    home.Won++;
                    home.Form.Add("W");
                }
                else if (match.HomeScore == match.AwayScore)
                {
                    home.Points += 1;
                    home.Drawn++;
                    home.Form.Add("D");
                }
                else
                {
                    home.Lost++;
                    home.Form.Add("L");
                }
            }

            if (accumulators.TryGetValue(match.AwayTeamId, out var away))
            {
                away.Played++;
                away.GoalsFor += match.AwayScore;
                away.GoalsAgainst += match.HomeScore;

                if (match.AwayScore > match.HomeScore)
                {
                    away.Points += 3;
                    away.Won++;
                    away.Form.Add("W");
                }
                else if (match.AwayScore == match.HomeScore)
                {
                    away.Points += 1;
                    away.Drawn++;
                    away.Form.Add("D");
                }
                else
                {
                    away.Lost++;
                    away.Form.Add("L");
                }
            }

            // Count cards from match events
            if (match.Events != null)
            {
                foreach (var evt in match.Events)
                {
                    if (accumulators.TryGetValue(evt.TeamId, out var teamAcc))
                    {
                        if (evt.Type == MatchEventType.YellowCard) teamAcc.YellowCards++;
                        else if (evt.Type == MatchEventType.RedCard) teamAcc.RedCards++;
                    }
                }
            }
        }

        // Rank: Group > Points > GD > GF > RedCards ASC > YellowCards ASC
        var sorted = accumulators.Values
            .OrderBy(s => s.GroupId ?? 0)
            .ThenByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalsFor - s.GoalsAgainst)
            .ThenByDescending(s => s.GoalsFor)
            .ThenBy(s => s.RedCards)
            .ThenBy(s => s.YellowCards)
            .ToList();

        // Assign rank per group
        int? currentGroup = int.MinValue;
        int rank = 0;

        var results = new List<TeamStanding>();
        foreach (var s in sorted)
        {
            if (s.GroupId != currentGroup)
            {
                currentGroup = s.GroupId;
                rank = 1;
            }
            else
            {
                rank++;
            }

            // Trim form to last 5
            var form = s.Form.Count > 5
                ? s.Form.Skip(s.Form.Count - 5).ToList()
                : s.Form;

            results.Add(new TeamStanding(
                TeamId: s.TeamId,
                TeamName: s.TeamName,
                GroupId: s.GroupId,
                Played: s.Played,
                Won: s.Won,
                Drawn: s.Drawn,
                Lost: s.Lost,
                GoalsFor: s.GoalsFor,
                GoalsAgainst: s.GoalsAgainst,
                GoalDifference: s.GoalsFor - s.GoalsAgainst,
                Points: s.Points,
                YellowCards: s.YellowCards,
                RedCards: s.RedCards,
                Rank: rank,
                Form: form));
        }

        return results;
    }

    /// <summary>
    /// Ranks a flat list of standings by the standard criteria.
    /// Used for cross-group comparisons (e.g., best 3rd-place teams).
    /// </summary>
    public static List<TeamStanding> Rank(List<TeamStanding> teams)
    {
        return teams
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalDifference)
            .ThenByDescending(s => s.GoalsFor)
            .ThenBy(s => s.RedCards)
            .ThenBy(s => s.YellowCards)
            .ToList();
    }

    // ── Lightweight projection types ──────────────────────────────────────────────
    // These allow the Application layer to project only the specific columns
    // StandingsCalculator needs from the DB, instead of loading full entity graphs.
    //
    // Before: SELECT * FROM Matches + LEFT JOIN MatchEvents (all 20+ columns each)
    // After:  SELECT only 7 match columns + 2 event columns
    // Estimated SQL bandwidth reduction: 65-80% for typical tournaments.

    /// <summary>Only the fields StandingsCalculator reads from a match event.</summary>
    public record MatchEventInput(Guid TeamId, MatchEventType Type);

    /// <summary>Only the 7 fields StandingsCalculator reads from a match.</summary>
    public record MatchInput(
        Guid HomeTeamId,
        Guid AwayTeamId,
        int HomeScore,
        int AwayScore,
        MatchStatus Status,
        int? GroupId,
        string? StageName,
        List<MatchEventInput> Events);

    /// <summary>Only the 3 fields StandingsCalculator reads from a team registration.</summary>
    public record RegistrationInput(Guid TeamId, string TeamName, int? GroupId);

    /// <summary>
    /// Overload for SQL-projected inputs — avoids loading full Match/TeamRegistration entity graphs.
    /// Semantically identical to Calculate(IEnumerable&lt;Match&gt;, IEnumerable&lt;TeamRegistration&gt;).
    /// Called from GetStandingsHandler via EF Core anonymous-type projection.
    /// </summary>
    public static List<TeamStanding> Calculate(
        IEnumerable<MatchInput> allMatches,
        IEnumerable<RegistrationInput> teams)
    {
        var accumulators = teams.ToDictionary(
            t => t.TeamId,
            t => new StandingAccumulator { TeamId = t.TeamId, TeamName = t.TeamName, GroupId = t.GroupId });

        var relevantMatches = allMatches.Where(m =>
            (m.GroupId != null || m.StageName == "League" || m.StageName == "Group Stage") &&
            m.Status == MatchStatus.Finished);

        foreach (var match in relevantMatches)
        {
            if (accumulators.TryGetValue(match.HomeTeamId, out var home))
            {
                home.Played++; home.GoalsFor += match.HomeScore; home.GoalsAgainst += match.AwayScore;
                if (match.HomeScore > match.AwayScore) { home.Points += 3; home.Won++; home.Form.Add("W"); }
                else if (match.HomeScore == match.AwayScore) { home.Points += 1; home.Drawn++; home.Form.Add("D"); }
                else { home.Lost++; home.Form.Add("L"); }
            }
            if (accumulators.TryGetValue(match.AwayTeamId, out var away))
            {
                away.Played++; away.GoalsFor += match.AwayScore; away.GoalsAgainst += match.HomeScore;
                if (match.AwayScore > match.HomeScore) { away.Points += 3; away.Won++; away.Form.Add("W"); }
                else if (match.AwayScore == match.HomeScore) { away.Points += 1; away.Drawn++; away.Form.Add("D"); }
                else { away.Lost++; away.Form.Add("L"); }
            }
            foreach (var evt in match.Events)
            {
                if (accumulators.TryGetValue(evt.TeamId, out var teamAcc))
                {
                    if (evt.Type == MatchEventType.YellowCard) teamAcc.YellowCards++;
                    else if (evt.Type == MatchEventType.RedCard) teamAcc.RedCards++;
                }
            }
        }

        var sorted = accumulators.Values
            .OrderBy(s => s.GroupId ?? 0)
            .ThenByDescending(s => s.Points)
            .ThenByDescending(s => s.GoalsFor - s.GoalsAgainst)
            .ThenByDescending(s => s.GoalsFor)
            .ThenBy(s => s.RedCards)
            .ThenBy(s => s.YellowCards)
            .ToList();

        int? currentGroup = int.MinValue;
        int rank = 0;
        var results = new List<TeamStanding>();
        foreach (var s in sorted)
        {
            if (s.GroupId != currentGroup) { currentGroup = s.GroupId; rank = 1; }
            else rank++;
            var form = s.Form.Count > 5 ? s.Form.Skip(s.Form.Count - 5).ToList() : s.Form;
            results.Add(new TeamStanding(
                TeamId: s.TeamId, TeamName: s.TeamName, GroupId: s.GroupId,
                Played: s.Played, Won: s.Won, Drawn: s.Drawn, Lost: s.Lost,
                GoalsFor: s.GoalsFor, GoalsAgainst: s.GoalsAgainst,
                GoalDifference: s.GoalsFor - s.GoalsAgainst,
                Points: s.Points, YellowCards: s.YellowCards, RedCards: s.RedCards,
                Rank: rank, Form: form));
        }
        return results;
    }

    private class StandingAccumulator
    {
        public Guid TeamId { get; init; }
        public string TeamName { get; init; } = "";
        public int? GroupId { get; init; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public List<string> Form { get; } = new();
    }
}
