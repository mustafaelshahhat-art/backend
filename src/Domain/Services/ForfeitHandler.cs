using Domain.Entities;
using Domain.Enums;

namespace Domain.Services;

/// <summary>
/// Pure domain service: handles match forfeit logic.
/// 
/// Extracted because identical forfeit code exists in:
/// - TournamentService.EliminateTeamAsync (sets matches to 3-0)
/// - TeamService.DisableTeamAsync (same logic copy-pasted)
/// 
/// ZERO dependencies — mutates Match entities in-place.
/// </summary>
public static class ForfeitHandler
{
    /// <summary>
    /// Forfeits all provided matches for the given team.
    /// Sets each match to Finished with 3-0 against the forfeited team.
    /// </summary>
    /// <returns>List of mutated matches (same references).</returns>
    public static List<Match> ForfeitMatches(IEnumerable<Match> upcomingMatches, Guid forfeitedTeamId)
    {
        var mutated = new List<Match>();

        foreach (var match in upcomingMatches)
        {
            if (match.Status != MatchStatus.Scheduled &&
                match.Status != MatchStatus.Live &&
                match.Status != MatchStatus.Postponed)
                continue;

            match.Status = MatchStatus.Finished;
            match.Forfeit = true;

            if (match.HomeTeamId == forfeitedTeamId)
            {
                match.HomeScore = 0;
                match.AwayScore = 3;
            }
            else if (match.AwayTeamId == forfeitedTeamId)
            {
                match.HomeScore = 3;
                match.AwayScore = 0;
            }

            mutated.Add(match);
        }

        return mutated;
    }
}
