using Application.Common.Models;
using Application.DTOs.Tournaments;
using Application.Features.Tournaments.Commands.ApproveRegistration;
using Application.Features.Tournaments.Commands.RejectRegistration;
using Application.Features.Tournaments.Commands.StartTournament;
using Application.Features.Tournaments.Commands.RegisterTeam;
using Application.Features.Tournaments.Commands.WithdrawTeam;
using Application.Features.Tournaments.Queries.GetTournamentsPaged;
using Application.Common.Interfaces;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using FluentAssertions;
using MediatR;
using MockQueryable;
using Moq;
using Match = Domain.Entities.Match;
using Shared.Exceptions;
using System.Linq.Expressions;
using Xunit;

namespace UnitTests.Handlers.Tournaments;

/// <summary>
/// Phase 3 gate: Tournament handler tests.
/// These tests validate the refactored handlers that use repositories directly
/// (no longer delegating to TournamentService).
/// Each test must pass before the corresponding service method can be removed.
/// </summary>
public class TournamentHandlerTests
{
    private readonly Mock<IRepository<Tournament>> _tournamentRepo = new();
    private readonly Mock<IRepository<TeamRegistration>> _registrationRepo = new();
    private readonly Mock<IRepository<Match>> _matchRepo = new();
    private readonly Mock<IRepository<Team>> _teamRepo = new();
    private readonly Mock<IRepository<TournamentPlayer>> _tournamentPlayerRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IDistributedLock> _distributedLock = new();
    private readonly Mock<ITransactionManager> _transactionManager = new();
    private readonly Mock<ITournamentRegistrationContext> _regContext = new();
    private readonly Mock<IActivityLogger> _activityLogger = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IRealTimeNotifier> _realTimeNotifier = new();
    private readonly Mock<IFileStorageService> _fileStorageService = new();

    public TournamentHandlerTests()
    {
        // Default: distributed lock always succeeds
        _distributedLock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _distributedLock.Setup(x => x.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Wire up the registration context facade
        _regContext.Setup(x => x.Tournaments).Returns(_tournamentRepo.Object);
        _regContext.Setup(x => x.Registrations).Returns(_registrationRepo.Object);
        _regContext.Setup(x => x.DistributedLock).Returns(_distributedLock.Object);
    }

    #region StartTournament

    [Fact]
    public async Task StartTournament_WithValidTournament_SetsStatusToActive()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var teamId1 = Guid.NewGuid();
        var teamId2 = Guid.NewGuid();

        var tournament = new Tournament
        {
            Id = tournamentId,
            Name = "Test Cup",
            CreatorUserId = userId,
            MinTeams = 2,
            MaxTeams = 8,
            SchedulingMode = SchedulingMode.Manual,
            Mode = TournamentMode.LeagueSingle
        };
        // Set to RegistrationClosed via the proper method
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen);
        tournament.ChangeStatus(TournamentStatus.RegistrationClosed);

        var approvedRegistrations = new List<TeamRegistration>
        {
            new TeamRegistration { Id = Guid.NewGuid(), TournamentId = tournamentId, TeamId = teamId1, Status = RegistrationStatus.Approved },
            new TeamRegistration { Id = Guid.NewGuid(), TournamentId = tournamentId, TeamId = teamId2, Status = RegistrationStatus.Approved }
        };

        // Existing matches exist (Manual mode - matches created manually)
        var existingMatches = new List<Match>
        {
            new Match { Id = Guid.NewGuid(), TournamentId = tournamentId, HomeTeamId = teamId1, AwayTeamId = teamId2, Status = MatchStatus.Scheduled }
        };

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _registrationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamRegistration, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvedRegistrations);
        _matchRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Match, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingMatches);
        _tournamentRepo.Setup(r => r.UpdateAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mapper.Setup(m => m.Map<TournamentDto>(It.IsAny<Tournament>()))
            .Returns((Tournament t) => new TournamentDto { Id = t.Id, Name = t.Name, Status = t.Status.ToString() });

        var handler = new StartTournamentCommandHandler(
            _tournamentRepo.Object, _matchRepo.Object, _registrationRepo.Object,
            _distributedLock.Object, _mapper.Object);

        // Act
        var result = await handler.Handle(
            new StartTournamentCommand(tournamentId, userId, UserRole.Admin.ToString()),
            CancellationToken.None);

        // Assert
        tournament.Status.Should().Be(TournamentStatus.Active);
        result.Should().NotBeNull();
        _tournamentRepo.Verify(r => r.UpdateAsync(tournament, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task StartTournament_WithInsufficientTeams_ThrowsBadRequest()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var tournament = new Tournament
        {
            Id = tournamentId,
            Name = "Empty Cup",
            CreatorUserId = userId,
            MinTeams = 4,
            MaxTeams = 16
        };
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen);
        tournament.ChangeStatus(TournamentStatus.RegistrationClosed);

        // Only 1 approved team — less than MinTeams (4)
        var approvedRegistrations = new List<TeamRegistration>
        {
            new TeamRegistration { Id = Guid.NewGuid(), TournamentId = tournamentId, TeamId = Guid.NewGuid(), Status = RegistrationStatus.Approved }
        };

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _registrationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamRegistration, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvedRegistrations);

        var handler = new StartTournamentCommandHandler(
            _tournamentRepo.Object, _matchRepo.Object, _registrationRepo.Object,
            _distributedLock.Object, _mapper.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new StartTournamentCommand(tournamentId, userId, UserRole.Admin.ToString()), CancellationToken.None));
    }

    #endregion

    #region ApproveRegistration

    [Fact]
    public async Task ApproveRegistration_ValidRegistration_SetsStatusToApproved()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var tournament = new Tournament
        {
            Id = tournamentId,
            Name = "Ramadan Cup",
            CreatorUserId = userId,
            MaxTeams = 8
        };

        var team = new Team
        {
            Id = teamId,
            Name = "Test FC",
            Players = new List<Player>
            {
                new Player { Id = playerId, Name = "Player 1", TeamId = teamId, TeamRole = TeamRole.Captain }
            }
        };

        var registration = new TeamRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            TeamId = teamId,
            Team = team,
            Status = RegistrationStatus.PendingPaymentReview
        };

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _registrationRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<TeamRegistration, bool>>>(),
            It.IsAny<string[]>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamRegistration> { registration });
        _tournamentPlayerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TournamentPlayer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentPlayer>());
        _tournamentPlayerRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<TournamentPlayer>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _registrationRepo.Setup(r => r.UpdateAsync(It.IsAny<TeamRegistration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _registrationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamRegistration, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamRegistration> { registration });
        _mapper.Setup(m => m.Map<TeamRegistrationDto>(It.IsAny<TeamRegistration>()))
            .Returns((TeamRegistration r) => new TeamRegistrationDto { Id = r.Id, TeamId = r.TeamId, Status = r.Status.ToString() });

        var handler = new ApproveRegistrationCommandHandler(
            _regContext.Object, _tournamentPlayerRepo.Object,
            _matchRepo.Object, _mapper.Object, _transactionManager.Object);

        // Act
        var result = await handler.Handle(
            new ApproveRegistrationCommand(tournamentId, teamId, userId, UserRole.Admin.ToString()),
            CancellationToken.None);

        // Assert
        registration.Status.Should().Be(RegistrationStatus.Approved);
        result.Should().NotBeNull();
        _tournamentPlayerRepo.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<TournamentPlayer>>(tp => tp.Any(p => p.PlayerId == playerId)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveRegistration_DuplicatePlayer_ThrowsConflict()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var playerId = Guid.NewGuid();

        var tournament = new Tournament
        {
            Id = tournamentId,
            Name = "Ramadan Cup",
            CreatorUserId = userId,
            MaxTeams = 8
        };

        var team = new Team
        {
            Id = teamId,
            Name = "Duplicate FC",
            Players = new List<Player>
            {
                new Player { Id = playerId, Name = "Already Enrolled", TeamId = teamId, TeamRole = TeamRole.Member }
            }
        };

        var registration = new TeamRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            TeamId = teamId,
            Team = team,
            Status = RegistrationStatus.PendingPaymentReview
        };

        // Player already exists in tournament via another team
        var existingParticipation = new TournamentPlayer
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            PlayerId = playerId,
            TeamId = Guid.NewGuid() // different team
        };

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _registrationRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<TeamRegistration, bool>>>(),
            It.IsAny<string[]>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamRegistration> { registration });
        _tournamentPlayerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TournamentPlayer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentPlayer> { existingParticipation });

        var handler = new ApproveRegistrationCommandHandler(
            _regContext.Object, _tournamentPlayerRepo.Object,
            _matchRepo.Object, _mapper.Object, _transactionManager.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(
                new ApproveRegistrationCommand(tournamentId, teamId, userId, UserRole.Admin.ToString()),
                CancellationToken.None));
    }

    #endregion

    #region RejectRegistration

    [Fact]
    public async Task RejectRegistration_ValidRegistration_SetsStatusToRejected()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var captainUserId = Guid.NewGuid();

        var tournament = new Tournament
        {
            Id = tournamentId,
            Name = "Rejection Cup",
            CreatorUserId = userId,
            MaxTeams = 8,
            CurrentTeams = 3
        };
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen);

        var captainPlayer = new Player { Id = Guid.NewGuid(), Name = "Captain", UserId = captainUserId, TeamRole = TeamRole.Captain, TeamId = teamId };
        var team = new Team { Id = teamId, Name = "Rejected FC", Players = new List<Player> { captainPlayer } };

        var registration = new TeamRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            TeamId = teamId,
            Team = team,
            Status = RegistrationStatus.PendingPaymentReview
        };

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _registrationRepo.Setup(r => r.FindAsync(
            It.IsAny<Expression<Func<TeamRegistration, bool>>>(),
            It.IsAny<string[]>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamRegistration> { registration });
        _registrationRepo.Setup(r => r.UpdateAsync(It.IsAny<TeamRegistration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tournamentPlayerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TournamentPlayer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentPlayer>());
        _tournamentPlayerRepo.Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<TournamentPlayer>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _matchRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Match, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Match>());
        _tournamentRepo.Setup(r => r.UpdateAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mapper.Setup(m => m.Map<TeamRegistrationDto>(It.IsAny<TeamRegistration>()))
            .Returns((TeamRegistration r) => new TeamRegistrationDto { Id = r.Id, Status = r.Status.ToString() });
        _mapper.Setup(m => m.Map<TournamentDto>(It.IsAny<Tournament>()))
            .Returns(new TournamentDto());
        _realTimeNotifier.Setup(n => n.SendTournamentUpdatedAsync(It.IsAny<TournamentDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RejectRegistrationCommandHandler(
            _regContext.Object, _tournamentPlayerRepo.Object,
            _matchRepo.Object, _realTimeNotifier.Object, _mapper.Object);

        // Act
        var result = await handler.Handle(
            new RejectRegistrationCommand(tournamentId, teamId, new RejectRegistrationRequest { Reason = "Payment invalid" }, userId, UserRole.Admin.ToString()),
            CancellationToken.None);

        // Assert
        registration.Status.Should().Be(RegistrationStatus.Rejected);
        registration.RejectionReason.Should().Be("Payment invalid");
        result.Should().NotBeNull();
    }

    #endregion

    #region WithdrawTeam

    [Fact]
    public async Task WithdrawTeam_CaptainWithdraws_SetsStatusToWithdrawn()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var captainUserId = Guid.NewGuid();

        var tournament = new Tournament
        {
            Id = tournamentId,
            Name = "Withdraw Cup",
            CurrentTeams = 5
        };
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen);

        var registration = new TeamRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            TeamId = teamId,
            Status = RegistrationStatus.Approved
        };

        var team = new Team
        {
            Id = teamId,
            Name = "Withdrawing FC",
            Players = new List<Player>
            {
                new Player { Id = Guid.NewGuid(), UserId = captainUserId, TeamRole = TeamRole.Captain, Name = "Captain" }
            }
        };

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _registrationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamRegistration, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamRegistration> { registration });
        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        _registrationRepo.Setup(r => r.UpdateAsync(It.IsAny<TeamRegistration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tournamentPlayerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TournamentPlayer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentPlayer>());
        _tournamentPlayerRepo.Setup(r => r.DeleteRangeAsync(It.IsAny<IEnumerable<TournamentPlayer>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tournamentRepo.Setup(r => r.UpdateAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _activityLogger.Setup(a => a.LogAsync(
            It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(),
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new WithdrawTeamCommandHandler(
            _regContext.Object, _teamRepo.Object,
            _tournamentPlayerRepo.Object, _activityLogger.Object);

        // Act
        await handler.Handle(new WithdrawTeamCommand(tournamentId, teamId, captainUserId), CancellationToken.None);

        // Assert
        registration.Status.Should().Be(RegistrationStatus.Withdrawn);
        tournament.CurrentTeams.Should().Be(4);
    }

    [Fact]
    public async Task WithdrawTeam_NonCaptain_ThrowsForbidden()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var nonCaptainUserId = Guid.NewGuid();

        var tournament = new Tournament
        {
            Id = tournamentId,
            Name = "Forbidden Cup"
        };
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen);

        var registration = new TeamRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            TeamId = teamId,
            Status = RegistrationStatus.Approved
        };

        // Team has a captain but the requesting user is NOT the captain
        var team = new Team
        {
            Id = teamId,
            Name = "Guarded FC",
            Players = new List<Player>
            {
                new Player { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), TeamRole = TeamRole.Captain, Name = "Real Captain" },
                new Player { Id = Guid.NewGuid(), UserId = nonCaptainUserId, TeamRole = TeamRole.Member, Name = "Member" }
            }
        };

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _registrationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TeamRegistration, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamRegistration> { registration });
        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = new WithdrawTeamCommandHandler(
            _regContext.Object, _teamRepo.Object,
            _tournamentPlayerRepo.Object, _activityLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new WithdrawTeamCommand(tournamentId, teamId, nonCaptainUserId), CancellationToken.None));
    }

    #endregion

    #region Query Delegation

    [Fact]
    public async Task GetTournamentsPaged_ReturnsPagedResult()
    {
        // Arrange
        var tournaments = new List<Tournament>
        {
            new Tournament { Id = Guid.NewGuid(), Name = "Cup 1", StartDate = DateTime.UtcNow.AddDays(1) },
            new Tournament { Id = Guid.NewGuid(), Name = "Cup 2", StartDate = DateTime.UtcNow.AddDays(2) },
            new Tournament { Id = Guid.NewGuid(), Name = "Cup 3", StartDate = DateTime.UtcNow.AddDays(3) }
        };
        // Set all to RegistrationOpen so they pass the non-draft filter
        foreach (var t in tournaments)
        {
            t.ChangeStatus(TournamentStatus.RegistrationOpen);
            t.Registrations = new List<TeamRegistration>();
            t.Matches = new List<Match>();
        }

        var mockQueryable = tournaments.AsQueryable().BuildMock();
        _tournamentRepo.Setup(r => r.GetQueryable()).Returns(mockQueryable);
        _tournamentRepo.Setup(r => r.ExecuteCountAsync(It.IsAny<IQueryable<Tournament>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // ExecuteQueryAsync<TResult> uses an anonymous type projection.
        // Use Moq's It.IsAnyType + InvocationFunc to dynamically materialize the query via reflection.
        _tournamentRepo
            .Setup(r => r.ExecuteQueryAsync(It.IsAny<IQueryable<It.IsAnyType>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryObj = invocation.Arguments[0];
                var queryableInterface = queryObj.GetType().GetInterfaces()
                    .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryable<>));
                var elementType = queryableInterface.GetGenericArguments()[0];
                var toListMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!.MakeGenericMethod(elementType);
                var list = toListMethod.Invoke(null, new[] { queryObj });
                var listType = typeof(List<>).MakeGenericType(elementType);
                var fromResultMethod = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(listType);
                return (Task)fromResultMethod.Invoke(null, new[] { list })!;
            }));

        _mapper.Setup(m => m.Map<TournamentDto>(It.IsAny<Tournament>()))
            .Returns((Tournament t) => new TournamentDto { Id = t.Id, Name = t.Name, Status = t.Status.ToString() });

        var handler = new GetTournamentsPagedQueryHandler(_tournamentRepo.Object, _userRepo.Object);

        // Act
        var result = await handler.Handle(
            new GetTournamentsPagedQuery(1, 10, null, UserRole.Admin.ToString()),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(3);
        result.PageNumber.Should().Be(1);
    }

    #endregion

    #region RegisterTeam

    private RegisterTeamCommandHandler CreateRegisterTeamHandler() =>
        new(_regContext.Object, _teamRepo.Object, _tournamentPlayerRepo.Object, _mapper.Object, _realTimeNotifier.Object);

    private Tournament CreateOpenTournament(Guid id, int maxTeams = 8, decimal entryFee = 100m)
    {
        var t = new Tournament
        {
            Id = id,
            Name = "Test Cup",
            MaxTeams = maxTeams,
            CurrentTeams = 0,
            EntryFee = entryFee,
            RegistrationDeadline = DateTime.UtcNow.AddDays(7),
            AllowLateRegistration = false,
            Registrations = new List<TeamRegistration>()
        };
        t.ChangeStatus(TournamentStatus.RegistrationOpen);
        return t;
    }

    private Team CreateTeamWithCaptain(Guid teamId, Guid captainUserId)
    {
        return new Team
        {
            Id = teamId,
            Name = "Test FC",
            Players = new List<Player>
            {
                new Player { Id = Guid.NewGuid(), UserId = captainUserId, TeamId = teamId, TeamRole = TeamRole.Captain, Name = "Captain" },
                new Player { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), TeamId = teamId, TeamRole = TeamRole.Member, Name = "Player2" }
            }
        };
    }

    [Fact]
    public async Task RegisterTeam_LockFails_ThrowsConflict()
    {
        _distributedLock.Setup(x => x.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterTeam_TournamentNotFound_ThrowsNotFound()
    {
        _tournamentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tournament?)null);

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterTeam_DeadlinePassed_ThrowsConflict()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = CreateOpenTournament(tournamentId);
        tournament.RegistrationDeadline = DateTime.UtcNow.AddDays(-1);

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(tournamentId, Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterTeam_TeamNotFound_ThrowsNotFound()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = CreateOpenTournament(tournamentId);

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _teamRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team?)null);

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(tournamentId, Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterTeam_NotCaptain_ThrowsForbidden()
    {
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var nonCaptainUserId = Guid.NewGuid();
        var tournament = CreateOpenTournament(tournamentId);
        var team = CreateTeamWithCaptain(teamId, Guid.NewGuid()); // captain is someone else

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(tournamentId, teamId, nonCaptainUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterTeam_AlreadyRegistered_ThrowsConflict()
    {
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var captainUserId = Guid.NewGuid();
        var tournament = CreateOpenTournament(tournamentId);
        tournament.Registrations.Add(new TeamRegistration
        {
            TournamentId = tournamentId,
            TeamId = teamId,
            Status = RegistrationStatus.PendingPaymentReview
        });
        var team = CreateTeamWithCaptain(teamId, captainUserId);

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(tournamentId, teamId, captainUserId);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterTeam_PaidTournament_StatusIsPendingPaymentReview()
    {
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var captainUserId = Guid.NewGuid();
        var tournament = CreateOpenTournament(tournamentId, entryFee: 500m);
        var team = CreateTeamWithCaptain(teamId, captainUserId);

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        _mapper.Setup(m => m.Map<TeamRegistrationDto>(It.IsAny<TeamRegistration>()))
            .Returns((TeamRegistration r) => new TeamRegistrationDto { Id = r.Id, Status = r.Status.ToString() });
        _mapper.Setup(m => m.Map<TournamentDto>(It.IsAny<Tournament>()))
            .Returns((Tournament t) => new TournamentDto { Id = t.Id });

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(tournamentId, teamId, captainUserId);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be(RegistrationStatus.PendingPaymentReview.ToString());
        _registrationRepo.Verify(r => r.AddAsync(It.IsAny<TeamRegistration>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTeam_FreeTournament_AutoApproved()
    {
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var captainUserId = Guid.NewGuid();
        var tournament = CreateOpenTournament(tournamentId, entryFee: 0m);
        var team = CreateTeamWithCaptain(teamId, captainUserId);

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        _tournamentPlayerRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<TournamentPlayer, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TournamentPlayer>());
        _mapper.Setup(m => m.Map<TeamRegistrationDto>(It.IsAny<TeamRegistration>()))
            .Returns((TeamRegistration r) => new TeamRegistrationDto { Id = r.Id, Status = r.Status.ToString() });
        _mapper.Setup(m => m.Map<TournamentDto>(It.IsAny<Tournament>()))
            .Returns((Tournament t) => new TournamentDto { Id = t.Id });

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(tournamentId, teamId, captainUserId);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.Status.Should().Be(RegistrationStatus.Approved.ToString());
        _tournamentPlayerRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<TournamentPlayer>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTeam_FullTournament_NoLateReg_ThrowsConflict()
    {
        var tournamentId = Guid.NewGuid();
        var tournament = CreateOpenTournament(tournamentId, maxTeams: 2);
        tournament.Registrations.Add(new TeamRegistration { Status = RegistrationStatus.Approved });
        tournament.Registrations.Add(new TeamRegistration { Status = RegistrationStatus.PendingPaymentReview });
        tournament.CurrentTeams = 2;

        var teamId = Guid.NewGuid();
        var captainUserId = Guid.NewGuid();
        var team = CreateTeamWithCaptain(teamId, captainUserId);

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(tournamentId, teamId, captainUserId);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterTeam_CapacityReached_ClosesRegistration()
    {
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var captainUserId = Guid.NewGuid();
        var tournament = CreateOpenTournament(tournamentId, maxTeams: 2, entryFee: 100m);
        tournament.Registrations.Add(new TeamRegistration { Status = RegistrationStatus.Approved });
        tournament.CurrentTeams = 1;

        var team = CreateTeamWithCaptain(teamId, captainUserId);

        _tournamentRepo.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tournament);
        _teamRepo.Setup(r => r.GetByIdAsync(teamId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(team);
        _mapper.Setup(m => m.Map<TeamRegistrationDto>(It.IsAny<TeamRegistration>()))
            .Returns((TeamRegistration r) => new TeamRegistrationDto { Id = r.Id, Status = r.Status.ToString() });
        _mapper.Setup(m => m.Map<TournamentDto>(It.IsAny<Tournament>()))
            .Returns((Tournament t) => new TournamentDto { Id = t.Id });

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(tournamentId, teamId, captainUserId);
        await handler.Handle(cmd, CancellationToken.None);

        tournament.Status.Should().Be(TournamentStatus.RegistrationClosed);
    }

    [Fact]
    public async Task RegisterTeam_LockAlwaysReleased()
    {
        _tournamentRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Not found"));

        var handler = CreateRegisterTeamHandler();
        var cmd = new RegisterTeamCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));

        _distributedLock.Verify(x => x.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
