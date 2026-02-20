using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;

namespace Application.Features.Tournaments;

/// <summary>
/// Pure static helpers for tournament operations, extracted from TournamentService.
/// No dependencies — all methods take data as parameters.
/// </summary>
public static class TournamentHelper
{
    /// <summary>
    /// Validates that the given user is the tournament creator or an Admin.
    /// </summary>
    public static void ValidateOwnership(Tournament tournament, Guid userId, string userRole)
    {
        var isAdmin = userRole == UserRole.Admin.ToString();
        var isOwner = userRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == userId;

        if (!isAdmin && !isOwner)
        {
            throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }
    }

    /// <summary>
    /// Calculates whether a tournament requires admin intervention based on its state.
    /// Pure function — no side effects.
    /// </summary>
    public static bool CheckInterventionRequired(Tournament tournament, int totalMatches, int finishedMatches, int totalRegs, int approvedRegs, DateTime now)
    {
        // Case 1: Registration closed but should be active
        if (tournament.Status == TournamentStatus.RegistrationClosed)
        {
            if (totalRegs == tournament.MaxTeams && approvedRegs == totalRegs && totalMatches == 0)
                return true;

            if (now > tournament.StartDate && totalMatches == 0)
                return true;
        }

        // Case 2: Active but stuck
        if (tournament.Status == TournamentStatus.Active)
        {
            if (totalMatches == 0) return true;
            if (totalMatches > 0 && totalMatches == finishedMatches) return true;
            if (now > tournament.EndDate.AddDays(2)) return true;
        }

        // Case 3: Completed but missing winner
        if (tournament.Status == TournamentStatus.Completed && tournament.WinnerTeamId == null && totalMatches > 0)
            return true;

        return false;
    }

    /// <summary>
    /// Creates a scheduled match entity with the given parameters. Does NOT persist.
    /// </summary>
    public static Match CreateGroupMatch(Tournament t, Guid h, Guid a, DateTime d, int? g, int? r, string s)
    {
        return new Match
        {
            TournamentId = t.Id,
            HomeTeamId = h,
            AwayTeamId = a,
            Status = MatchStatus.Scheduled,
            Date = d,
            GroupId = g,
            RoundNumber = r,
            StageName = s,
            HomeScore = 0,
            AwayScore = 0
        };
    }

    /// <summary>
    /// Checks if two teams are the designated opening pair for a tournament.
    /// </summary>
    public static bool IsOpeningPair(Tournament tournament, Guid teamId1, Guid teamId2)
    {
        if (!tournament.HasOpeningTeams) return false;
        var a = tournament.OpeningTeamAId!.Value;
        var b = tournament.OpeningTeamBId!.Value;
        return (teamId1 == a && teamId2 == b) || (teamId1 == b && teamId2 == a);
    }

    /// <summary>
    /// Core match generation: given a tournament (with Registrations loaded) and approved team IDs,
    /// computes all matches for Groups/Knockout/League modes.
    /// Mutates tournament.Registrations (sets GroupId for Groups mode).
    /// Returns matches (NOT persisted — caller must call AddRangeAsync).
    /// </summary>
    public static List<Match> CreateMatches(Tournament tournament, List<Guid> teamIds)
    {
        var matches = new List<Match>();
        var random = new Random();
        var matchDate = DateTime.UtcNow.AddDays(2);
        var effectiveMode = tournament.GetEffectiveMode();

        if (effectiveMode == TournamentMode.GroupsKnockoutSingle || effectiveMode == TournamentMode.GroupsKnockoutHomeAway)
        {
            if (tournament.NumberOfGroups < 1) tournament.NumberOfGroups = 1;

            var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();

            var distribution = Domain.Services.GroupDistributionAlgorithm.Distribute(
                shuffledTeams,
                tournament.NumberOfGroups,
                tournament.HasOpeningTeams ? tournament.OpeningTeamAId : null,
                tournament.HasOpeningTeams ? tournament.OpeningTeamBId : null);

            var validation = Domain.Services.GroupDistributionAlgorithm.Validate(
                distribution, shuffledTeams, tournament.NumberOfGroups);
            if (!validation.IsValid)
                throw new InvalidOperationException($"Group distribution failed: {string.Join("; ", validation.Errors)}");

            var groups = Enumerable.Range(1, distribution.Count)
                .Select(g => distribution[g])
                .ToList();

            // Persistent Group Assignment (in-memory, saved by caller's UpdateAsync)
            foreach (var (groupId, groupTeamIds) in distribution)
            {
                foreach (var teamId in groupTeamIds)
                {
                    var reg = tournament.Registrations.FirstOrDefault(r => r.TeamId == teamId);
                    if (reg != null) reg.GroupId = groupId;
                }
            }

            int dayOffset = 0;
            bool isHomeAway = effectiveMode == TournamentMode.GroupsKnockoutHomeAway;

            for (int g = 0; g < groups.Count; g++)
            {
                var groupTeams = groups[g];
                bool isOpeningGroup = tournament.HasOpeningTeams &&
                    groupTeams.Contains(tournament.OpeningTeamAId!.Value) &&
                    groupTeams.Contains(tournament.OpeningTeamBId!.Value);

                var groupMatchList = new List<Match>();

                for (int i = 0; i < groupTeams.Count; i++)
                {
                    for (int j = i + 1; j < groupTeams.Count; j++)
                    {
                        var match = CreateGroupMatch(tournament, groupTeams[i], groupTeams[j], matchDate.AddDays(dayOffset), g + 1, 1, "Group Stage");

                        if (isOpeningGroup && IsOpeningPair(tournament, groupTeams[i], groupTeams[j]))
                            match.IsOpeningMatch = true;

                        groupMatchList.Add(match);
                        dayOffset++;

                        if (isHomeAway)
                        {
                            groupMatchList.Add(CreateGroupMatch(tournament, groupTeams[j], groupTeams[i], matchDate.AddDays(dayOffset + 2), g + 1, 1, "Group Stage"));
                            dayOffset++;
                        }
                    }
                }

                if (isOpeningGroup)
                {
                    var openingMatch = groupMatchList.FirstOrDefault(m => m.IsOpeningMatch);
                    if (openingMatch != null)
                    {
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
                }
                matches.AddRange(groupMatchList);
            }
        }
        else if (effectiveMode == TournamentMode.KnockoutSingle || effectiveMode == TournamentMode.KnockoutHomeAway)
        {
            List<Guid> shuffledTeams;
            if (tournament.HasOpeningTeams)
            {
                var openingTeamA = tournament.OpeningTeamAId!.Value;
                var openingTeamB = tournament.OpeningTeamBId!.Value;
                var remaining = teamIds.Where(id => id != openingTeamA && id != openingTeamB).OrderBy(x => random.Next()).ToList();
                shuffledTeams = new List<Guid> { openingTeamA, openingTeamB };
                shuffledTeams.AddRange(remaining);
            }
            else
            {
                shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
            }

            bool isHomeAway = effectiveMode == TournamentMode.KnockoutHomeAway;
            for (int i = 0; i < shuffledTeams.Count; i += 2)
            {
                if (i + 1 < shuffledTeams.Count)
                {
                    var match = CreateGroupMatch(tournament, shuffledTeams[i], shuffledTeams[i + 1], matchDate.AddDays(i), null, 1, "Round 1");
                    if (tournament.HasOpeningTeams && i == 0) match.IsOpeningMatch = true;
                    matches.Add(match);
                    if (isHomeAway) matches.Add(CreateGroupMatch(tournament, shuffledTeams[i + 1], shuffledTeams[i], matchDate.AddDays(i + 3), null, 1, "Round 1"));
                }
            }
        }
        else // League modes
        {
            List<Guid> orderedTeams;
            if (tournament.HasOpeningTeams)
            {
                var openingTeamA = tournament.OpeningTeamAId!.Value;
                var openingTeamB = tournament.OpeningTeamBId!.Value;
                var remaining = teamIds.Where(id => id != openingTeamA && id != openingTeamB).OrderBy(x => random.Next()).ToList();
                orderedTeams = new List<Guid> { openingTeamA, openingTeamB };
                orderedTeams.AddRange(remaining);
            }
            else
            {
                orderedTeams = teamIds.OrderBy(x => random.Next()).ToList();
            }

            bool isHomeAway = effectiveMode == TournamentMode.LeagueHomeAway;
            int matchCount = 0;
            bool openingMatchSet = false;

            for (int i = 0; i < orderedTeams.Count; i++)
            {
                for (int j = i + 1; j < orderedTeams.Count; j++)
                {
                    var match = CreateGroupMatch(tournament, orderedTeams[i], orderedTeams[j], matchDate.AddDays(matchCount * 2), 1, 1, "League");
                    if (!openingMatchSet && tournament.HasOpeningTeams && IsOpeningPair(tournament, orderedTeams[i], orderedTeams[j]))
                    {
                        match.IsOpeningMatch = true;
                        openingMatchSet = true;
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
                        matches.Add(CreateGroupMatch(tournament, orderedTeams[j], orderedTeams[i], matchDate.AddDays(matchCount * 2 + 1), 1, 1, "League"));
                        matchCount++;
                    }
                }
            }
        }

        return matches;
    }

    /// <summary>
    /// Creates matches for manual group draw. Tournament must have Registrations loaded with GroupId assigned.
    /// Returns matches (NOT persisted).
    /// </summary>
    public static List<Match> CreateManualGroupMatches(Tournament tournament)
    {
        var registrations = tournament.Registrations.Where(r => r.Status == RegistrationStatus.Approved).ToList();
        var groups = registrations.GroupBy(r => r.GroupId!.Value).ToList();
        var matches = new List<Match>();
        var matchDate = tournament.StartDate.AddHours(18);
        bool isHomeAway = tournament.GetEffectiveMode() == TournamentMode.GroupsKnockoutHomeAway ||
                          tournament.GetEffectiveMode() == TournamentMode.LeagueHomeAway;

        foreach (var group in groups)
        {
            var teamIds = group.Select(r => r.TeamId).ToList();
            var groupMatchList = new List<Match>();

            for (int i = 0; i < teamIds.Count; i++)
            {
                for (int j = i + 1; j < teamIds.Count; j++)
                {
                    var match = CreateGroupMatch(tournament, teamIds[i], teamIds[j], matchDate, group.Key, 1, "Group Stage");
                    if (IsOpeningPair(tournament, teamIds[i], teamIds[j])) match.IsOpeningMatch = true;
                    groupMatchList.Add(match);
                    matchDate = matchDate.AddHours(2);

                    if (isHomeAway)
                    {
                        groupMatchList.Add(CreateGroupMatch(tournament, teamIds[j], teamIds[i], matchDate.AddDays(2), group.Key, 1, "Group Stage"));
                    }
                }
            }

            // Reorder: Opening match first in its group
            var opening = groupMatchList.FirstOrDefault(m => m.IsOpeningMatch);
            if (opening != null)
            {
                groupMatchList.Remove(opening);
                if (groupMatchList.Count > 0)
                {
                    var firstDate = groupMatchList.Min(m => m.Date);
                    var openingDate = opening.Date;
                    var firstMatch = groupMatchList.FirstOrDefault(m => m.Date == firstDate);
                    if (firstMatch != null) { firstMatch.Date = openingDate; opening.Date = firstDate; }
                }
                groupMatchList.Insert(0, opening);
            }
            matches.AddRange(groupMatchList);
        }

        return matches;
    }
}
