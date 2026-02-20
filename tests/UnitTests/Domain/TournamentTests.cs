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

    // ══════════════════════════════════════════════════════════════════════
    // RequiresManualDraw — domain policy tests
    // ══════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)] // Semi-final for an 8-team bracket
    public void RequiresManualDraw_ManualMode_NonFinalRound_ReturnsTrue(int roundNumber)
    {
        // Arrange — Manual scheduling mode
        var tournament = new Tournament
        {
            SchedulingMode = SchedulingMode.Manual
        };

        // Act
        var result = tournament.RequiresManualDraw(roundNumber, isFinalRound: false);

        // Assert
        result.Should().BeTrue(
            "Manual mode tournaments require organiser pairings for all non-final rounds.");
    }

    [Fact]
    public void RequiresManualDraw_ManualMode_FinalRound_ReturnsFalse()
    {
        // Arrange — tournament is Manual but this IS the Final
        var tournament = new Tournament
        {
            SchedulingMode = SchedulingMode.Manual
        };

        // Act
        var result = tournament.RequiresManualDraw(roundNumber: 4, isFinalRound: true);

        // Assert
        result.Should().BeFalse(
            "The Final is ALWAYS auto-generated, even in Manual mode.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void RequiresManualDraw_AutomaticMode_AnyRound_ReturnsFalse(int roundNumber)
    {
        // Arrange — Random / Automatic scheduling mode
        var tournament = new Tournament
        {
            SchedulingMode = SchedulingMode.Random
        };

        // Act
        var result = tournament.RequiresManualDraw(roundNumber, isFinalRound: false);

        // Assert
        result.Should().BeFalse(
            "In Automatic mode the lifecycle service generates all rounds; RequiresManualDraw must never block it.");
    }

    [Fact]
    public void RequiresManualDraw_AutomaticMode_FinalRound_ReturnsFalse()
    {
        // Belt-and-braces: Automatic + Final → still false
        var tournament = new Tournament
        {
            SchedulingMode = SchedulingMode.Random
        };

        var result = tournament.RequiresManualDraw(roundNumber: 3, isFinalRound: true);

        result.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════════════
    // RequiresManualQualification — domain policy tests
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Advances a freshly-created (Draft) tournament to the target status by
    /// walking the only valid state-machine path for each destination.
    /// </summary>
    private static Tournament BuildTournament(
        TournamentStatus targetStatus,
        SchedulingMode schedulingMode = SchedulingMode.Random)
    {
        var tournament = new Tournament { SchedulingMode = schedulingMode };
        // Draft is the starting state; walk the path to each destination.
        var path = targetStatus switch
        {
            TournamentStatus.Draft                     => Array.Empty<TournamentStatus>(),
            TournamentStatus.RegistrationOpen          => new[] { TournamentStatus.RegistrationOpen },
            TournamentStatus.RegistrationClosed        => new[] { TournamentStatus.RegistrationOpen,  TournamentStatus.RegistrationClosed },
            TournamentStatus.Active                    => new[] { TournamentStatus.RegistrationOpen,  TournamentStatus.RegistrationClosed, TournamentStatus.Active },
            TournamentStatus.ManualQualificationPending => new[] { TournamentStatus.RegistrationOpen, TournamentStatus.RegistrationClosed, TournamentStatus.Active, TournamentStatus.ManualQualificationPending },
            TournamentStatus.QualificationConfirmed    => new[] { TournamentStatus.RegistrationOpen,  TournamentStatus.RegistrationClosed, TournamentStatus.Active, TournamentStatus.ManualQualificationPending, TournamentStatus.QualificationConfirmed },
            _ => throw new NotSupportedException($"Add a path for {targetStatus} in the test helper.")
        };
        foreach (var step in path)
            tournament.ChangeStatus(step);
        return tournament;
    }

    [Fact]
    public void RequiresManualQualification_ManualMode_PendingStatus_ReturnsTrue()
    {
        // Arrange — Manual mode + group stage done → pending status
        var tournament = BuildTournament(
            TournamentStatus.ManualQualificationPending,
            SchedulingMode.Manual);

        // Act
        var result = tournament.RequiresManualQualification();

        // Assert
        result.Should().BeTrue(
            "A Manual-mode tournament in ManualQualificationPending status must block automatic knockout seeding.");
    }

    [Fact]
    public void RequiresManualQualification_ManualMode_ConfirmedStatus_ReturnsFalse()
    {
        // Arrange — organiser already confirmed; lifecycle service should proceed
        var tournament = BuildTournament(
            TournamentStatus.QualificationConfirmed,
            SchedulingMode.Manual);

        // Act
        var result = tournament.RequiresManualQualification();

        // Assert
        result.Should().BeFalse(
            "QualificationConfirmed means the organiser has acted; knockout generation is unblocked.");
    }

    [Fact]
    public void RequiresManualQualification_AutomaticMode_ReturnsFalse()
    {
        // Arrange — Automatic mode; status happens to be ManualQualificationPending
        // (unlikely in production, but the flag should still be false)
        var tournament = BuildTournament(
            TournamentStatus.ManualQualificationPending,
            SchedulingMode.Random);

        // Act
        var result = tournament.RequiresManualQualification();

        // Assert
        result.Should().BeFalse(
            "Automatic mode tournaments never require organiser qualification; standings drive the process.");
    }

    [Fact]
    public void RequiresManualQualification_ManualMode_ActiveStatus_ReturnsFalse()
    {
        // Arrange — Active status means the tournament is mid-group or already in knockout
        var tournament = BuildTournament(
            TournamentStatus.Active,
            SchedulingMode.Manual);

        // Act
        var result = tournament.RequiresManualQualification();

        // Assert
        result.Should().BeFalse(
            "Active status is not ManualQualificationPending; gate must not fire during group play.");
    }
}
