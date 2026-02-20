using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using FluentAssertions;
using Xunit;

namespace UnitTests.Domain;

public class StandingsCalculatorTests
{
    #region Helpers

    private static Guid TeamId(int n) => new Guid($"00000000-0000-0000-0000-{n:D12}");

    private static TeamRegistration Reg(int teamNum, int? groupId = 1, string teamName = "")
    {
        var teamId = TeamId(teamNum);
        return new TeamRegistration
        {
            TeamId = teamId,
            GroupId = groupId,
            Team = new Team { Id = teamId, Name = string.IsNullOrEmpty(teamName) ? $"Team {teamNum}" : teamName }
        };
    }

    private static Match FinishedMatch(int home, int away, int homeScore, int awayScore,
        int? groupId = 1, string stageName = "Group Stage", List<MatchEvent>? events = null)
    {
        return new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = Guid.NewGuid(),
            HomeTeamId = TeamId(home),
            AwayTeamId = TeamId(away),
            HomeScore = homeScore,
            AwayScore = awayScore,
            Status = MatchStatus.Finished,
            GroupId = groupId,
            StageName = stageName,
            Events = events ?? new List<MatchEvent>()
        };
    }

    private static MatchEvent Card(MatchEventType type, int teamNum, int minute = 45)
    {
        return new MatchEvent
        {
            Type = type,
            TeamId = TeamId(teamNum),
            Minute = minute
        };
    }

    #endregion

    [Fact]
    public void Calculate_NoMatches_AllTeamsHaveZeroPoints()
    {
        // Arrange
        var teams = new[] { Reg(1), Reg(2), Reg(3) };
        var matches = Array.Empty<Match>();

        // Act
        var standings = StandingsCalculator.Calculate(matches, teams);

        // Assert
        standings.Should().HaveCount(3);
        standings.Should().AllSatisfy(s =>
        {
            s.Points.Should().Be(0);
            s.Played.Should().Be(0);
            s.Won.Should().Be(0);
            s.Drawn.Should().Be(0);
            s.Lost.Should().Be(0);
            s.GoalsFor.Should().Be(0);
            s.GoalsAgainst.Should().Be(0);
            s.GoalDifference.Should().Be(0);
        });
    }

    [Fact]
    public void Calculate_SingleGroup_AllMatchesPlayed_CorrectPointsAndGD()
    {
        // Arrange: 3 teams, round-robin
        // T1 beats T2 (2-1), T1 beats T3 (1-0), T2 beats T3 (3-0)
        var teams = new[] { Reg(1), Reg(2), Reg(3) };
        var matches = new[]
        {
            FinishedMatch(1, 2, 2, 1),  // T1 wins
            FinishedMatch(1, 3, 1, 0),  // T1 wins
            FinishedMatch(2, 3, 3, 0),  // T2 wins
        };

        // Act
        var standings = StandingsCalculator.Calculate(matches, teams);

        // Assert
        // T1: 6pts, GD=+2 (GF=3, GA=1)
        var t1 = standings.First(s => s.TeamId == TeamId(1));
        t1.Points.Should().Be(6);
        t1.Won.Should().Be(2);
        t1.Drawn.Should().Be(0);
        t1.Lost.Should().Be(0);
        t1.GoalsFor.Should().Be(3);
        t1.GoalsAgainst.Should().Be(1);
        t1.GoalDifference.Should().Be(2);
        t1.Played.Should().Be(2);
        t1.Rank.Should().Be(1);

        // T2: 3pts, GD=+1 (GF=4, GA=2)
        var t2 = standings.First(s => s.TeamId == TeamId(2));
        t2.Points.Should().Be(3);
        t2.Won.Should().Be(1);
        t2.Lost.Should().Be(1);
        t2.GoalsFor.Should().Be(4);
        t2.GoalsAgainst.Should().Be(2);
        t2.GoalDifference.Should().Be(2);
        t2.Rank.Should().Be(2);

        // T3: 0pts, GD=-4 (GF=0, GA=4)
        var t3 = standings.First(s => s.TeamId == TeamId(3));
        t3.Points.Should().Be(0);
        t3.Won.Should().Be(0);
        t3.Lost.Should().Be(2);
        t3.GoalsFor.Should().Be(0);
        t3.GoalsAgainst.Should().Be(4);
        t3.GoalDifference.Should().Be(-4);
        t3.Rank.Should().Be(3);
    }

    [Fact]
    public void Calculate_Tiebreaker_GoalDifferenceThenGoalsFor()
    {
        // Arrange: T1 and T2 both have 3 pts, T1 has better GD
        var teams = new[] { Reg(1), Reg(2), Reg(3) };
        var matches = new[]
        {
            FinishedMatch(1, 3, 4, 0),  // T1 wins big
            FinishedMatch(2, 3, 1, 0),  // T2 wins small
            FinishedMatch(1, 2, 0, 1),  // T2 beats T1
        };

        // Act
        var standings = StandingsCalculator.Calculate(matches, teams);

        // T1: 3pts, GD=+3 (GF=4, GA=1)
        // T2: 6pts, GD=+2 (GF=2, GA=0) - wait, T2 has 2 wins
        // Actually: T2 wins vs T3 and vs T1 = 6 pts. T1 wins vs T3 only = 3 pts.
        var t2 = standings.First(s => s.TeamId == TeamId(2));
        t2.Points.Should().Be(6);
        t2.Rank.Should().Be(1);

        var t1 = standings.First(s => s.TeamId == TeamId(1));
        t1.Points.Should().Be(3);
        t1.Rank.Should().Be(2);
    }

    [Fact]
    public void Calculate_Tiebreaker_SamePointsSameGD_RankedByGoalsFor()
    {
        // Arrange: T1 and T2 tied on points and GD, but T1 has more GF
        var teams = new[] { Reg(1), Reg(2), Reg(3), Reg(4) };
        var matches = new[]
        {
            FinishedMatch(1, 3, 3, 1),  // T1 wins (GF=3, GA=1, GD=+2)
            FinishedMatch(2, 4, 2, 0),  // T2 wins (GF=2, GA=0, GD=+2)
            FinishedMatch(1, 2, 1, 1),  // Draw
        };

        var standings = StandingsCalculator.Calculate(matches, teams);

        // T1: 4pts, GD=+2, GF=4 → rank 1
        // T2: 4pts, GD=+2, GF=3 → rank 2
        var t1 = standings.First(s => s.TeamId == TeamId(1));
        var t2 = standings.First(s => s.TeamId == TeamId(2));
        t1.Points.Should().Be(4);
        t2.Points.Should().Be(4);
        t1.GoalDifference.Should().Be(2);
        t2.GoalDifference.Should().Be(2);
        t1.GoalsFor.Should().BeGreaterThan(t2.GoalsFor);
        t1.Rank.Should().BeLessThan(t2.Rank);
    }

    [Fact]
    public void Calculate_Tiebreaker_CardsDiscipline()
    {
        // Arrange: Same points, GD, GF — differ by cards
        var teams = new[] { Reg(1), Reg(2) };
        var matches = new[]
        {
            FinishedMatch(1, 2, 1, 1, events: new List<MatchEvent>
            {
                Card(MatchEventType.YellowCard, 1),
                Card(MatchEventType.YellowCard, 1),
                Card(MatchEventType.RedCard, 1),
                Card(MatchEventType.YellowCard, 2),
            }),
        };

        var standings = StandingsCalculator.Calculate(matches, teams);

        // T1: 1pt, GD=0, GF=1, RC=1, YC=2
        // T2: 1pt, GD=0, GF=1, RC=0, YC=1
        // T2 ranks higher (fewer cards)
        var t1 = standings.First(s => s.TeamId == TeamId(1));
        var t2 = standings.First(s => s.TeamId == TeamId(2));
        t1.RedCards.Should().Be(1);
        t1.YellowCards.Should().Be(2);
        t2.RedCards.Should().Be(0);
        t2.YellowCards.Should().Be(1);
        t2.Rank.Should().BeLessThan(t1.Rank); // T2 ranked higher
    }

    [Fact]
    public void Calculate_IgnoresNonFinishedMatches()
    {
        var teams = new[] { Reg(1), Reg(2) };
        var matches = new[]
        {
            new Match
            {
                HomeTeamId = TeamId(1), AwayTeamId = TeamId(2),
                HomeScore = 5, AwayScore = 0,
                Status = MatchStatus.Scheduled, // Not finished!
                GroupId = 1, StageName = "Group Stage",
                Events = new List<MatchEvent>()
            }
        };

        var standings = StandingsCalculator.Calculate(matches, teams);

        standings.Should().AllSatisfy(s => s.Played.Should().Be(0));
    }

    [Fact]
    public void Calculate_IgnoresKnockoutMatches()
    {
        var teams = new[] { Reg(1), Reg(2) };
        var matches = new[]
        {
            new Match
            {
                HomeTeamId = TeamId(1), AwayTeamId = TeamId(2),
                HomeScore = 2, AwayScore = 0,
                Status = MatchStatus.Finished,
                GroupId = null, StageName = "Round 1", // Knockout, not group
                Events = new List<MatchEvent>()
            }
        };

        var standings = StandingsCalculator.Calculate(matches, teams);

        standings.Should().AllSatisfy(s => s.Played.Should().Be(0));
    }

    [Fact]
    public void Calculate_LeagueMatches_AreIncluded()
    {
        var teams = new[] { Reg(1, groupId: null), Reg(2, groupId: null) };
        var matches = new[]
        {
            new Match
            {
                HomeTeamId = TeamId(1), AwayTeamId = TeamId(2),
                HomeScore = 2, AwayScore = 1,
                Status = MatchStatus.Finished,
                GroupId = null, StageName = "League",
                Events = new List<MatchEvent>()
            }
        };

        var standings = StandingsCalculator.Calculate(matches, teams);

        var t1 = standings.First(s => s.TeamId == TeamId(1));
        t1.Played.Should().Be(1);
        t1.Points.Should().Be(3);
    }

    [Fact]
    public void Calculate_FormTrimsToLastFive()
    {
        // Arrange: 7 matches so form should be trimmed to last 5
        var teams = new[] { Reg(1), Reg(2) };
        var matches = new List<Match>();
        for (int i = 0; i < 7; i++)
        {
            matches.Add(FinishedMatch(1, 2, 1, 0)); // T1 always wins
        }

        var standings = StandingsCalculator.Calculate(matches, teams);

        var t1 = standings.First(s => s.TeamId == TeamId(1));
        t1.Form.Should().HaveCount(5);
        t1.Form.Should().AllBe("W");
    }

    [Fact]
    public void Calculate_Draw_BothTeamsGetOnePoint()
    {
        var teams = new[] { Reg(1), Reg(2) };
        var matches = new[] { FinishedMatch(1, 2, 2, 2) };

        var standings = StandingsCalculator.Calculate(matches, teams);

        standings.Should().AllSatisfy(s =>
        {
            s.Points.Should().Be(1);
            s.Drawn.Should().Be(1);
            s.Won.Should().Be(0);
            s.Lost.Should().Be(0);
        });
    }

    [Fact]
    public void Calculate_MultipleGroups_RanksIndependently()
    {
        // Group 1: T1, T2. Group 2: T3, T4
        var teams = new[] { Reg(1, groupId: 1), Reg(2, groupId: 1), Reg(3, groupId: 2), Reg(4, groupId: 2) };
        var matches = new[]
        {
            FinishedMatch(1, 2, 0, 1, groupId: 1),  // T2 wins group 1
            FinishedMatch(3, 4, 0, 1, groupId: 2),  // T4 wins group 2
        };

        var standings = StandingsCalculator.Calculate(matches, teams);

        // Both group winners should be rank 1 in their groups
        var t2 = standings.First(s => s.TeamId == TeamId(2));
        var t4 = standings.First(s => s.TeamId == TeamId(4));
        t2.Rank.Should().Be(1);
        t4.Rank.Should().Be(1);

        var t1 = standings.First(s => s.TeamId == TeamId(1));
        var t3 = standings.First(s => s.TeamId == TeamId(3));
        t1.Rank.Should().Be(2);
        t3.Rank.Should().Be(2);
    }

    [Fact]
    public void Rank_SortsCorrectly()
    {
        var standings = new List<StandingsCalculator.TeamStanding>
        {
            new(TeamId(1), "T1", 1, 3, 1, 0, 2, 2, 4, -2, 3, 0, 0, 0, new()),
            new(TeamId(2), "T2", 1, 3, 2, 0, 1, 5, 2, 3, 6, 1, 0, 0, new()),
            new(TeamId(3), "T3", 1, 3, 3, 0, 0, 7, 1, 6, 9, 0, 0, 0, new()),
        };

        var ranked = StandingsCalculator.Rank(standings);

        ranked[0].TeamId.Should().Be(TeamId(3)); // 9 pts
        ranked[1].TeamId.Should().Be(TeamId(2)); // 6 pts
        ranked[2].TeamId.Should().Be(TeamId(1)); // 3 pts
    }
}
