using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using FluentAssertions;
using Xunit;

namespace UnitTests.Domain;

public class MatchGeneratorTests
{
    private static Guid TournamentId => Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static Guid TeamId(int n) => new Guid($"00000000-0000-0000-0000-{n:D12}");
    private static List<Guid> TeamIds(int count) => Enumerable.Range(1, count).Select(TeamId).ToList();
    private static readonly DateTime BaseDate = new(2025, 1, 1);
    private static readonly Random FixedRandom = new(42); // Deterministic

    private static MatchGenerator.MatchGenerationConfig Config(
        TournamentMode mode, int groups = 1,
        bool hasOpening = false, Guid? openA = null, Guid? openB = null)
    {
        return new MatchGenerator.MatchGenerationConfig(
            TournamentId, mode, groups, hasOpening, openA, openB, BaseDate);
    }

    #region League Mode

    [Fact]
    public void Generate_LeagueSingle_4Teams_Creates6Matches()
    {
        // 4 teams, round-robin single = C(4,2) = 6 matches
        var result = MatchGenerator.Generate(
            Config(TournamentMode.LeagueSingle), TeamIds(4), new Random(42));

        result.Matches.Should().HaveCount(6);
        result.Matches.Should().AllSatisfy(m =>
        {
            m.TournamentId.Should().Be(TournamentId);
            m.Status.Should().Be(MatchStatus.Scheduled);
            m.StageName.Should().Be("League");
        });
    }

    [Fact]
    public void Generate_LeagueHomeAway_4Teams_Creates12Matches()
    {
        // 4 teams home+away = C(4,2)*2 = 12 matches
        var result = MatchGenerator.Generate(
            Config(TournamentMode.LeagueHomeAway), TeamIds(4), new Random(42));

        result.Matches.Should().HaveCount(12);
    }

    [Fact]
    public void Generate_League_OpeningMatch_IsMarked()
    {
        var teams = TeamIds(4);
        var config = Config(TournamentMode.LeagueSingle, hasOpening: true,
            openA: teams[0], openB: teams[1]);

        var result = MatchGenerator.Generate(config, teams, new Random(42));

        result.Matches.Should().Contain(m => m.IsOpeningMatch);
        var openingMatch = result.Matches.First(m => m.IsOpeningMatch);
        var pair = new[] { openingMatch.HomeTeamId, openingMatch.AwayTeamId };
        pair.Should().Contain(teams[0]);
        pair.Should().Contain(teams[1]);
    }

    #endregion

    #region Knockout Mode

    [Fact]
    public void Generate_KnockoutSingle_4Teams_Creates2Matches()
    {
        // 4 teams, single knockout = 2 first-round matches
        var result = MatchGenerator.Generate(
            Config(TournamentMode.KnockoutSingle), TeamIds(4), new Random(42));

        result.Matches.Should().HaveCount(2);
        result.Matches.Should().AllSatisfy(m => m.StageName.Should().Be("Round 1"));
        result.GroupAssignments.Should().BeEmpty();
    }

    [Fact]
    public void Generate_KnockoutHomeAway_4Teams_Creates4Matches()
    {
        // 4 teams, home+away knockout = 4 first-round matches (2 per pair)
        var result = MatchGenerator.Generate(
            Config(TournamentMode.KnockoutHomeAway), TeamIds(4), new Random(42));

        result.Matches.Should().HaveCount(4);
    }

    [Fact]
    public void Generate_Knockout_OpeningMatch_IsFirst()
    {
        var teams = TeamIds(4);
        var config = Config(TournamentMode.KnockoutSingle, hasOpening: true,
            openA: teams[0], openB: teams[1]);

        var result = MatchGenerator.Generate(config, teams, new Random(42));

        result.Matches.First().IsOpeningMatch.Should().BeTrue();
        result.Matches.First().HomeTeamId.Should().Be(teams[0]);
        result.Matches.First().AwayTeamId.Should().Be(teams[1]);
    }

    #endregion

    #region Group Stage Mode

    [Fact]
    public void Generate_GroupsKnockoutSingle_4Teams_2Groups_Creates2GroupMatches()
    {
        // 4 teams split into 2 groups of 2 = 1 match per group = 2 total
        var result = MatchGenerator.Generate(
            Config(TournamentMode.GroupsKnockoutSingle, groups: 2), TeamIds(4), new Random(42));

        result.Matches.Should().HaveCount(2);
        result.Matches.Should().AllSatisfy(m => m.StageName.Should().Be("Group Stage"));
        result.GroupAssignments.Should().HaveCount(4);
    }

    [Fact]
    public void Generate_GroupsKnockoutSingle_6Teams_2Groups_Creates6GroupMatches()
    {
        // 6 teams split into 2 groups of 3 = C(3,2) * 2 = 3*2 = 6 matches
        var result = MatchGenerator.Generate(
            Config(TournamentMode.GroupsKnockoutSingle, groups: 2), TeamIds(6), new Random(42));

        result.Matches.Should().HaveCount(6);
        result.GroupAssignments.Should().HaveCount(6);
    }

    [Fact]
    public void Generate_GroupStage_AllTeamsAssignedToGroup()
    {
        var teams = TeamIds(8);
        var result = MatchGenerator.Generate(
            Config(TournamentMode.GroupsKnockoutSingle, groups: 2), teams, new Random(42));

        result.GroupAssignments.Should().HaveCount(8);
        result.GroupAssignments.Values.Distinct().Should().HaveCount(2);
    }

    [Fact]
    public void Generate_GroupStage_WithOpeningTeams_BothInSameGroup()
    {
        var teams = TeamIds(8);
        var config = Config(TournamentMode.GroupsKnockoutSingle, groups: 2,
            hasOpening: true, openA: teams[0], openB: teams[1]);

        var result = MatchGenerator.Generate(config, teams, new Random(42));

        // Opening teams should be in the same group
        var groupA = result.GroupAssignments[teams[0]];
        var groupB = result.GroupAssignments[teams[1]];
        groupA.Should().Be(groupB);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Generate_AllMatchesHaveUniqueTeamPairs()
    {
        var result = MatchGenerator.Generate(
            Config(TournamentMode.LeagueSingle), TeamIds(4), new Random(42));

        var pairs = result.Matches.Select(m =>
        {
            var ids = new[] { m.HomeTeamId, m.AwayTeamId }.OrderBy(x => x).ToList();
            return $"{ids[0]}-{ids[1]}";
        }).ToList();

        pairs.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_AllMatchesHaveScheduledDates()
    {
        var result = MatchGenerator.Generate(
            Config(TournamentMode.LeagueSingle), TeamIds(4), new Random(42));

        result.Matches.Should().AllSatisfy(m => m.Date.Should().NotBeNull());
    }

    [Fact]
    public void Generate_GroupStage_ZeroGroups_ReturnsEmpty()
    {
        var config = Config(TournamentMode.GroupsKnockoutSingle, groups: 0);
        var result = MatchGenerator.Generate(config, TeamIds(4), new Random(42));

        result.Matches.Should().BeEmpty();
    }

    #endregion
}
