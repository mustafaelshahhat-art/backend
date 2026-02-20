using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using FluentAssertions;
using Xunit;

namespace UnitTests.Domain;

public class KnockoutBracketBuilderTests
{
    private static Guid TeamId(int n) => new Guid($"00000000-0000-0000-0000-{n:D12}");

    #region DetermineQualifiedTeams

    [Fact]
    public void DetermineQualifiedTeams_TwoGroupsOfTwo_TopOneFromEach()
    {
        var standings = new List<StandingsCalculator.TeamStanding>
        {
            new(TeamId(1), "T1", 1, 1, 1, 0, 0, 2, 0, 2, 3, 0, 0, 1, new()),
            new(TeamId(2), "T2", 1, 1, 0, 0, 1, 0, 2, -2, 0, 0, 0, 2, new()),
            new(TeamId(3), "T3", 2, 1, 1, 0, 0, 1, 0, 1, 3, 0, 0, 1, new()),
            new(TeamId(4), "T4", 2, 1, 0, 0, 1, 0, 1, -1, 0, 0, 0, 2, new()),
        };

        var qualified = KnockoutBracketBuilder.DetermineQualifiedTeams(standings);

        // 2 groups of 2 → top 1 from each = 2 qualified
        qualified.Should().HaveCount(2);
        qualified.Should().Contain(q => q.TeamId == TeamId(1) && q.GroupRank == 1);
        qualified.Should().Contain(q => q.TeamId == TeamId(3) && q.GroupRank == 1);
    }

    [Fact]
    public void DetermineQualifiedTeams_GroupOfThree_TopTwoQualify()
    {
        var standings = new List<StandingsCalculator.TeamStanding>
        {
            new(TeamId(1), "T1", 1, 2, 2, 0, 0, 4, 0, 4, 6, 0, 0, 1, new()),
            new(TeamId(2), "T2", 1, 2, 1, 0, 1, 2, 2, 0, 3, 0, 0, 2, new()),
            new(TeamId(3), "T3", 1, 2, 0, 0, 2, 0, 4, -4, 0, 0, 0, 3, new()),
        };

        var qualified = KnockoutBracketBuilder.DetermineQualifiedTeams(standings);

        // 1 group of 3 → top 2 qualify
        qualified.Should().HaveCountGreaterOrEqualTo(2);
        qualified.Should().Contain(q => q.TeamId == TeamId(1));
        qualified.Should().Contain(q => q.TeamId == TeamId(2));
    }

    #endregion

    #region CreatePairings

    [Fact]
    public void CreatePairings_FourTeams_CreatesTwoPairings()
    {
        var qualified = new List<KnockoutBracketBuilder.QualifiedTeam>
        {
            new(TeamId(1), 1, 1),
            new(TeamId(2), 1, 2),
            new(TeamId(3), 2, 1),
            new(TeamId(4), 2, 2),
        };

        var result = KnockoutBracketBuilder.CreatePairings(qualified, random: new Random(42));

        result.Pairings.Should().HaveCount(2);
        var allTeams = result.Pairings
            .SelectMany(p => new[] { p.HomeTeamId, p.AwayTeamId })
            .ToList();
        allTeams.Should().HaveCount(4);
        allTeams.Distinct().Should().HaveCount(4);
    }

    [Fact]
    public void CreatePairings_PrefersCrossGroupPairings()
    {
        // 4 teams from 2 groups — should prefer cross-group
        var qualified = new List<KnockoutBracketBuilder.QualifiedTeam>
        {
            new(TeamId(1), 1, 1),
            new(TeamId(2), 2, 1),
            new(TeamId(3), 1, 2),
            new(TeamId(4), 2, 2),
        };

        // Run multiple times with different seeds to verify tendency
        int crossGroupCount = 0;
        for (int seed = 0; seed < 20; seed++)
        {
            var result = KnockoutBracketBuilder.CreatePairings(qualified, random: new Random(seed));
            foreach (var p in result.Pairings)
            {
                var homeGroup = qualified.First(q => q.TeamId == p.HomeTeamId).GroupId;
                var awayGroup = qualified.First(q => q.TeamId == p.AwayTeamId).GroupId;
                if (homeGroup != awayGroup) crossGroupCount++;
            }
        }

        // With 4 teams from 2 groups, most pairings should be cross-group
        crossGroupCount.Should().BeGreaterThan(20); // Out of 40 total pairings (20 runs * 2 per run)
    }

    [Fact]
    public void CreatePairings_WithOpeningTeams_SeparatesOpeningPairing()
    {
        var qualified = new List<KnockoutBracketBuilder.QualifiedTeam>
        {
            new(TeamId(1), 1, 1),
            new(TeamId(2), 2, 1),
            new(TeamId(3), 1, 2),
            new(TeamId(4), 2, 2),
        };

        var result = KnockoutBracketBuilder.CreatePairings(qualified,
            openingHomeTeamId: TeamId(1), openingAwayTeamId: TeamId(2),
            random: new Random(42));

        result.OpeningPairing.Should().NotBeNull();
        result.OpeningPairing!.HomeTeamId.Should().Be(TeamId(1));
        result.OpeningPairing!.AwayTeamId.Should().Be(TeamId(2));

        // Remaining teams in regular pairings
        result.Pairings.Should().HaveCount(1);
        var remainingTeams = result.Pairings.SelectMany(p => new[] { p.HomeTeamId, p.AwayTeamId }).ToList();
        remainingTeams.Should().Contain(TeamId(3));
        remainingTeams.Should().Contain(TeamId(4));
    }

    #endregion

    #region DetermineWinners

    [Fact]
    public void DetermineWinners_SingleLeg_HigherScoreWins()
    {
        var matches = new List<Match>
        {
            new() { Id = Guid.NewGuid(), HomeTeamId = TeamId(1), AwayTeamId = TeamId(2), HomeScore = 2, AwayScore = 1, Events = new List<MatchEvent>() },
            new() { Id = Guid.NewGuid(), HomeTeamId = TeamId(3), AwayTeamId = TeamId(4), HomeScore = 0, AwayScore = 3, Events = new List<MatchEvent>() },
        };

        var winners = KnockoutBracketBuilder.DetermineWinners(matches);

        winners.Should().HaveCount(2);
        winners.Should().Contain(TeamId(1));
        winners.Should().Contain(TeamId(4));
    }

    [Fact]
    public void DetermineWinners_TwoLegs_AggregateScoreDecides()
    {
        var matches = new List<Match>
        {
            // Leg 1: T1 vs T2 → 1-0
            new() { Id = Guid.NewGuid(), HomeTeamId = TeamId(1), AwayTeamId = TeamId(2), HomeScore = 1, AwayScore = 0, Events = new List<MatchEvent>() },
            // Leg 2: T2 vs T1 → 2-0 (T2 aggregate: 2+0=2, T1 aggregate: 1+0=1)
            new() { Id = Guid.NewGuid(), HomeTeamId = TeamId(2), AwayTeamId = TeamId(1), HomeScore = 2, AwayScore = 0, Events = new List<MatchEvent>() },
        };

        var winners = KnockoutBracketBuilder.DetermineWinners(matches);

        winners.Should().HaveCount(1);
        winners.Should().Contain(TeamId(2)); // T2 wins on aggregate 2-1
    }

    [Fact]
    public void DetermineWinners_TiedOnAggregate_HomeTeamFirstLegWins()
    {
        // If aggregate is tied, code picks agg1 >= agg2 → match.HomeTeamId (first leg home)
        var matches = new List<Match>
        {
            new() { Id = Guid.NewGuid(), HomeTeamId = TeamId(1), AwayTeamId = TeamId(2), HomeScore = 1, AwayScore = 1, Events = new List<MatchEvent>() },
            new() { Id = Guid.NewGuid(), HomeTeamId = TeamId(2), AwayTeamId = TeamId(1), HomeScore = 1, AwayScore = 1, Events = new List<MatchEvent>() },
        };

        var winners = KnockoutBracketBuilder.DetermineWinners(matches);

        // Agg: T1 = 1+1=2, T2 = 1+1=2 → tie → agg1 >= agg2 → T1 (first leg home)
        winners.Should().HaveCount(1);
        winners.Should().Contain(TeamId(1));
    }

    #endregion

    #region NextPowerOfTwo

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    [InlineData(8, 8)]
    [InlineData(9, 16)]
    [InlineData(16, 16)]
    [InlineData(17, 32)]
    [InlineData(32, 32)]
    [InlineData(33, 64)]
    public void NextPowerOfTwo_ReturnsCorrectValue(int input, int expected)
    {
        KnockoutBracketBuilder.NextPowerOfTwo(input).Should().Be(expected);
    }

    #endregion
}
