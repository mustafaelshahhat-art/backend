using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Xunit;

namespace UnitTests.Domain;

public class TournamentTests
{
    [Fact]
    public void GetEffectiveMode_ShouldReturnMode_WhenModeIsSet()
    {
        // Arrange
        var tournament = new Tournament
        {
            Mode = TournamentMode.GroupsKnockoutHomeAway
        };

        // Act
        var result = tournament.GetEffectiveMode();

        // Assert
        result.Should().Be(TournamentMode.GroupsKnockoutHomeAway);
    }

    [Theory]
    [InlineData(TournamentFormat.RoundRobin, false, TournamentLegType.SingleLeg, TournamentMode.LeagueSingle)]
    [InlineData(TournamentFormat.RoundRobin, true, TournamentLegType.SingleLeg, TournamentMode.LeagueHomeAway)]
    [InlineData(TournamentFormat.KnockoutOnly, false, TournamentLegType.SingleLeg, TournamentMode.KnockoutSingle)]
    [InlineData(TournamentFormat.KnockoutOnly, true, TournamentLegType.SingleLeg, TournamentMode.KnockoutHomeAway)]
    public void GetEffectiveMode_ShouldMapCorrectly_WhenModeIsNull(
        TournamentFormat format, bool homeAway, TournamentLegType legType, TournamentMode expected)
    {
        // Arrange
        var tournament = new Tournament
        {
            Format = format,
            IsHomeAwayEnabled = homeAway,
            MatchType = legType
        };

        // Act
        var result = tournament.GetEffectiveMode();

        // Assert
        result.Should().Be(expected);
    }
}
