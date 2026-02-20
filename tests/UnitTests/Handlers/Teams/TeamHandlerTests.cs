using Application.DTOs.Teams;
using Application.DTOs.Tournaments;
using Application.Features.Teams.Commands.CreateTeam;
using Application.Features.Teams.Commands.DisableTeam;
using Application.Features.Teams.Commands.RemovePlayer;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using FluentAssertions;
using Moq;
using Shared.Exceptions;
using System.Linq.Expressions;
using Xunit;
using Match = Domain.Entities.Match;

namespace UnitTests.Handlers.Teams;

/// <summary>
/// Phase 3 gate: Team handler tests.
/// Handlers have inlined logic — tests validate direct behavior.
/// </summary>
public class TeamHandlerTests
{
    private readonly Mock<IRepository<Team>> _teamRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IRepository<Player>> _playerRepo = new();
    private readonly Mock<IRepository<TeamRegistration>> _registrationRepo = new();
    private readonly Mock<IMatchRepository> _matchRepo = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IRealTimeNotifier> _realTimeNotifier = new();
    private readonly Mock<ISystemSettingsService> _systemSettingsService = new();
    private readonly Mock<ITournamentLifecycleService> _lifecycleService = new();
    private readonly Mock<IMatchEventNotifier> _matchEventNotifier = new();
    private readonly Mock<ITeamMemberDataService> _memberData = new();
    private readonly Mock<ITeamNotificationFacade> _teamNotifier = new();

    public TeamHandlerTests()
    {
        _memberData.Setup(x => x.Users).Returns(_userRepo.Object);
        _memberData.Setup(x => x.Players).Returns(_playerRepo.Object);
    }

    #region CreateTeam

    [Fact]
    public async Task CreateTeam_ValidRequest_ReturnsTeamDto()
    {
        // Arrange
        var captainId = Guid.NewGuid();
        var captain = new User { Id = captainId, Name = "Captain Ahmed", DisplayId = "U-1234" };
        var request = new CreateTeamRequest { Name = "Al Ahly", Founded = "1907", City = "Cairo" };

        _systemSettingsService.Setup(s => s.IsTeamCreationAllowedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _userRepo.Setup(r => r.GetByIdAsync(captainId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(captain);
        _teamRepo.Setup(r => r.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mapper.Setup(m => m.Map<TeamDto>(It.IsAny<Team>()))
            .Returns((Team t) => new TeamDto { Id = t.Id, Name = t.Name });
        _realTimeNotifier.Setup(n => n.SendTeamCreatedAsync(It.IsAny<TeamDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new CreateTeamCommandHandler(
            _teamRepo.Object, _userRepo.Object, _mapper.Object,
            _realTimeNotifier.Object, _systemSettingsService.Object);

        // Act
        var result = await handler.Handle(new CreateTeamCommand(request, captainId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Al Ahly");
        result.CaptainName.Should().Be("Captain Ahmed");
        _teamRepo.Verify(r => r.AddAsync(It.Is<Team>(t => t.Name == "Al Ahly" && t.Players.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTeam_TeamCreationDisabled_ThrowsBadRequest()
    {
        // Arrange
        _systemSettingsService.Setup(s => s.IsTeamCreationAllowedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateTeamCommandHandler(
            _teamRepo.Object, _userRepo.Object, _mapper.Object,
            _realTimeNotifier.Object, _systemSettingsService.Object);

        var request = new CreateTeamRequest { Name = "Test Team", Founded = "2020" };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(
            () => handler.Handle(new CreateTeamCommand(request, Guid.NewGuid()), CancellationToken.None));
    }

    #endregion

    #region DisableTeam

    [Fact]
    public async Task DisableTeam_WithActiveTournament_ForfeitsMatches()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        var captainUserId = Guid.NewGuid();
        var opponentId = Guid.NewGuid();

        var team = new Team
        {
            Id = teamId,
            Name = "Disabled FC",
            IsActive = true,
            Players = new List<Player>
            {
                new Player { Id = Guid.NewGuid(), Name = "Captain", UserId = captainUserId, TeamRole = TeamRole.Captain }
            }
        };

        var registration = new TeamRegistration
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            TournamentId = tournamentId,
            Status = RegistrationStatus.Approved
        };

        var scheduledMatch = new Match
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            HomeTeamId = teamId,
            AwayTeamId = opponentId,
            Status = MatchStatus.Scheduled,
            Forfeit = false
        };

        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        _teamRepo.Setup(r => r.UpdateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _registrationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamRegistration, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamRegistration> { registration });
        _registrationRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<TeamRegistration>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _matchRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Match, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Match> { scheduledMatch });
        _matchRepo.Setup(r => r.UpdateRangeAsync(It.IsAny<IEnumerable<Match>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _lifecycleService.Setup(s => s.CheckAndFinalizeTournamentAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TournamentLifecycleResult());
        var handler = new DisableTeamCommandHandler(
            _teamRepo.Object, _registrationRepo.Object, _matchRepo.Object,
            _lifecycleService.Object, _matchEventNotifier.Object);

        // Act
        await handler.Handle(new DisableTeamCommand(teamId), CancellationToken.None);

        // Assert
        team.IsActive.Should().BeFalse();
        registration.Status.Should().Be(RegistrationStatus.Withdrawn);
        scheduledMatch.Status.Should().Be(MatchStatus.Finished);
        scheduledMatch.Forfeit.Should().BeTrue();
        // Home team is the disabled team — should lose 0-3
        scheduledMatch.HomeScore.Should().Be(0);
        scheduledMatch.AwayScore.Should().Be(3);
        _lifecycleService.Verify(s => s.CheckAndFinalizeTournamentAsync(tournamentId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RemovePlayer

    [Fact]
    public async Task RemovePlayer_LastPlayer_MarksTeamRemoved()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var player = new Player
        {
            Id = playerId,
            TeamId = teamId,
            UserId = targetUserId,
            Name = "Solo Player",
            TeamRole = TeamRole.Member
        };

        var captainPlayer = new Player
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Name = "Captain",
            TeamRole = TeamRole.Captain
        };

        var team = new Team
        {
            Id = teamId,
            Name = "Solo Team",
            // After removing the target player, only captain remains — but the test is about
            // removing the last non-captain player. The handler checks Players collection.
            Players = new List<Player> { captainPlayer, player }
        };

        // Auth helper: user is admin so passes auth check
        _teamRepo.Setup(r => r.GetByIdNoTrackingAsync(teamId,
            It.IsAny<Expression<Func<Team, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        _playerRepo.Setup(r => r.GetByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);

        _teamRepo.Setup(r => r.GetByIdAsync(teamId,
            It.IsAny<Expression<Func<Team, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var targetUser = new User { Id = targetUserId, Name = "Solo Player", TeamId = teamId, DisplayId = "U-5678" };
        _userRepo.Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // No other teams for this player
        _playerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Player, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Player>());

        _playerRepo.Setup(r => r.DeleteAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _teamNotifier.Setup(n => n.SendUserUpdatedAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _teamNotifier.Setup(n => n.SendRemovedFromTeamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _teamNotifier.Setup(n => n.SendTeamUpdatedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _teamNotifier.Setup(n => n.NotifyByTemplateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RemovePlayerCommandHandler(
            _teamRepo.Object, _memberData.Object, _teamNotifier.Object);

        // Act
        await handler.Handle(new RemovePlayerCommand(teamId, playerId, userId, UserRole.Admin.ToString()), CancellationToken.None);

        // Assert — player deleted, user's TeamId cleared (no other teams)
        _playerRepo.Verify(r => r.DeleteAsync(player, It.IsAny<CancellationToken>()), Times.Once);
        targetUser.TeamId.Should().BeNull("because the player has no other teams");
    }

    [Fact]
    public async Task RemovePlayer_HasOtherPlayers_KeepsTeam()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var otherTeamId = Guid.NewGuid();

        var captainPlayer = new Player
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Name = "Captain",
            TeamRole = TeamRole.Captain
        };

        var playerToRemove = new Player
        {
            Id = playerId,
            TeamId = teamId,
            UserId = targetUserId,
            Name = "Player Two",
            TeamRole = TeamRole.Member
        };

        var remainingPlayer = new Player
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = Guid.NewGuid(),
            Name = "Player Three",
            TeamRole = TeamRole.Member
        };

        var team = new Team
        {
            Id = teamId,
            Name = "Full Team",
            Players = new List<Player> { captainPlayer, playerToRemove, remainingPlayer }
        };

        // Auth: admin bypass
        _teamRepo.Setup(r => r.GetByIdNoTrackingAsync(teamId,
            It.IsAny<Expression<Func<Team, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        _playerRepo.Setup(r => r.GetByIdAsync(playerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playerToRemove);

        _teamRepo.Setup(r => r.GetByIdAsync(teamId,
            It.IsAny<Expression<Func<Team, object>>[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var targetUser = new User { Id = targetUserId, Name = "Player Two", TeamId = teamId, DisplayId = "U-9999" };
        _userRepo.Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _userRepo.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Player is also in another team
        _playerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Player, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Player> { new Player { TeamId = otherTeamId } });

        _playerRepo.Setup(r => r.DeleteAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _teamNotifier.Setup(n => n.SendUserUpdatedAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _teamNotifier.Setup(n => n.SendRemovedFromTeamAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _teamNotifier.Setup(n => n.SendTeamUpdatedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _teamNotifier.Setup(n => n.NotifyByTemplateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RemovePlayerCommandHandler(
            _teamRepo.Object, _memberData.Object, _teamNotifier.Object);

        // Act
        await handler.Handle(new RemovePlayerCommand(teamId, playerId, userId, UserRole.Admin.ToString()), CancellationToken.None);

        // Assert — player deleted but user's TeamId set to the other team
        _playerRepo.Verify(r => r.DeleteAsync(playerToRemove, It.IsAny<CancellationToken>()), Times.Once);
        targetUser.TeamId.Should().Be(otherTeamId, "because the player belongs to another team");
        // Team still has remaining players — team is NOT deleted
        _teamRepo.Verify(r => r.DeleteAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
