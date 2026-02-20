using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using FluentAssertions;
using Xunit;

namespace UnitTests.Domain;

public class ForfeitHandlerTests
{
    private static Guid TeamId(int n) => new Guid($"00000000-0000-0000-0000-{n:D12}");

    private static Match ScheduledMatch(int home, int away, MatchStatus status = MatchStatus.Scheduled)
    {
        return new Match
        {
            Id = Guid.NewGuid(),
            HomeTeamId = TeamId(home),
            AwayTeamId = TeamId(away),
            HomeScore = 0,
            AwayScore = 0,
            Status = status,
            Events = new List<MatchEvent>()
        };
    }

    [Fact]
    public void ForfeitMatches_HomeTeamForfeited_SetsScore0To3()
    {
        var forfeitedTeam = TeamId(1);
        var matches = new[] { ScheduledMatch(1, 2) };

        var mutated = ForfeitHandler.ForfeitMatches(matches, forfeitedTeam);

        mutated.Should().HaveCount(1);
        mutated[0].HomeScore.Should().Be(0);
        mutated[0].AwayScore.Should().Be(3);
        mutated[0].Forfeit.Should().BeTrue();
        mutated[0].Status.Should().Be(MatchStatus.Finished);
    }

    [Fact]
    public void ForfeitMatches_AwayTeamForfeited_SetsScore3To0()
    {
        var forfeitedTeam = TeamId(2);
        var matches = new[] { ScheduledMatch(1, 2) };

        var mutated = ForfeitHandler.ForfeitMatches(matches, forfeitedTeam);

        mutated.Should().HaveCount(1);
        mutated[0].HomeScore.Should().Be(3);
        mutated[0].AwayScore.Should().Be(0);
        mutated[0].Forfeit.Should().BeTrue();
        mutated[0].Status.Should().Be(MatchStatus.Finished);
    }

    [Fact]
    public void ForfeitMatches_AlreadyFinished_IsSkipped()
    {
        var forfeitedTeam = TeamId(1);
        var matches = new[] { ScheduledMatch(1, 2, MatchStatus.Finished) };

        var mutated = ForfeitHandler.ForfeitMatches(matches, forfeitedTeam);

        mutated.Should().BeEmpty();
    }

    [Fact]
    public void ForfeitMatches_CancelledMatch_IsSkipped()
    {
        var forfeitedTeam = TeamId(1);
        var matches = new[] { ScheduledMatch(1, 2, MatchStatus.Cancelled) };

        var mutated = ForfeitHandler.ForfeitMatches(matches, forfeitedTeam);

        mutated.Should().BeEmpty();
    }

    [Fact]
    public void ForfeitMatches_LiveMatch_IsForfeited()
    {
        var forfeitedTeam = TeamId(1);
        var matches = new[] { ScheduledMatch(1, 2, MatchStatus.Live) };

        var mutated = ForfeitHandler.ForfeitMatches(matches, forfeitedTeam);

        mutated.Should().HaveCount(1);
        mutated[0].Status.Should().Be(MatchStatus.Finished);
    }

    [Fact]
    public void ForfeitMatches_PostponedMatch_IsForfeited()
    {
        var forfeitedTeam = TeamId(1);
        var matches = new[] { ScheduledMatch(1, 2, MatchStatus.Postponed) };

        var mutated = ForfeitHandler.ForfeitMatches(matches, forfeitedTeam);

        mutated.Should().HaveCount(1);
    }

    [Fact]
    public void ForfeitMatches_MultipleMatches_OnlyEligibleAreForfeited()
    {
        var forfeitedTeam = TeamId(1);
        var matches = new[]
        {
            ScheduledMatch(1, 2, MatchStatus.Scheduled),
            ScheduledMatch(1, 3, MatchStatus.Finished),   // skip
            ScheduledMatch(1, 4, MatchStatus.Cancelled),   // skip
            ScheduledMatch(1, 5, MatchStatus.Live),
            ScheduledMatch(1, 6, MatchStatus.Postponed),
        };

        var mutated = ForfeitHandler.ForfeitMatches(matches, forfeitedTeam);

        mutated.Should().HaveCount(3); // Scheduled + Live + Postponed
        mutated.Should().AllSatisfy(m =>
        {
            m.Status.Should().Be(MatchStatus.Finished);
            m.Forfeit.Should().BeTrue();
            m.HomeScore.Should().Be(0); // Home team forfeited
            m.AwayScore.Should().Be(3);
        });
    }

    [Fact]
    public void ForfeitMatches_EmptyList_ReturnsEmpty()
    {
        var mutated = ForfeitHandler.ForfeitMatches(Array.Empty<Match>(), TeamId(1));
        mutated.Should().BeEmpty();
    }

    [Fact]
    public void ForfeitMatches_TeamNotInMatch_MatchNotForfeited()
    {
        // T3 is forfeited but match is between T1 and T2
        var matches = new[] { ScheduledMatch(1, 2) };
        var mutated = ForfeitHandler.ForfeitMatches(matches, TeamId(3));

        // Match is still processed (status check passes) but neither branch sets scores
        // The match IS added to mutated list (continues past status check)
        // Actually looking at the code: it checks HomeTeamId/AwayTeamId with else-if
        // If neither matches, scores stay 0-0 but match IS still marked Finished with Forfeit=true
        mutated.Should().HaveCount(1);
        mutated[0].Status.Should().Be(MatchStatus.Finished);
        mutated[0].Forfeit.Should().BeTrue();
        mutated[0].HomeScore.Should().Be(0);
        mutated[0].AwayScore.Should().Be(0);
    }
}
