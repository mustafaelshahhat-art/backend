using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using TournamentHelper = global::Application.Features.Tournaments.TournamentHelper;

namespace UnitTests.Application;

/// <summary>
/// TournamentService has been decommissioned. Registration logic is now in
/// RegisterTeamCommandHandler. These test the extracted TournamentHelper.
/// </summary>
public class TournamentHandlerInlineTests
{
    [Fact]
    public void TournamentHelper_CheckInterventionRequired_ReturnsTrueForStuckActive()
    {
        // Active tournament with no matches → requires intervention
        var tournament = new Tournament();
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen);
        tournament.ChangeStatus(TournamentStatus.RegistrationClosed);
        tournament.ChangeStatus(TournamentStatus.Active);
        var result = TournamentHelper.CheckInterventionRequired(
            tournament, totalMatches: 0, finishedMatches: 0, totalRegs: 4, approvedRegs: 4, DateTime.UtcNow);
        result.Should().BeTrue();
    }

    [Fact]
    public void TournamentHelper_CheckInterventionRequired_ReturnsFalseForHealthyActive()
    {
        var tournament = new Tournament
        {
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen);
        tournament.ChangeStatus(TournamentStatus.RegistrationClosed);
        tournament.ChangeStatus(TournamentStatus.Active);
        var result = TournamentHelper.CheckInterventionRequired(
            tournament, totalMatches: 6, finishedMatches: 2, totalRegs: 4, approvedRegs: 4, DateTime.UtcNow);
        result.Should().BeFalse();
    }
}
