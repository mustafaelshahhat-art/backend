using Application.Interfaces;
using Application.Services;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using FluentAssertions;
using Moq;
using Shared.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;
using AutoMapper;

namespace UnitTests.Application;

public class TournamentServiceTests
{
    private readonly Mock<IRepository<Tournament>> _tournamentRepoMock;
    private readonly Mock<IRepository<TeamRegistration>> _registrationRepoMock;
    private readonly Mock<IRepository<Team>> _teamRepoMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly TournamentService _service;

    public TournamentServiceTests()
    {
        _tournamentRepoMock = new Mock<IRepository<Tournament>>();
        _registrationRepoMock = new Mock<IRepository<TeamRegistration>>();
        _teamRepoMock = new Mock<IRepository<Team>>();
        _mapperMock = new Mock<IMapper>();

        _service = new TournamentService(
            _tournamentRepoMock.Object,
            _registrationRepoMock.Object,
            new Mock<global::Domain.Interfaces.IRepository<global::Domain.Entities.Match>>().Object,
            _mapperMock.Object,
            new Mock<IAnalyticsService>().Object,
            new Mock<INotificationService>().Object,
            _teamRepoMock.Object,
            new Mock<IRealTimeNotifier>().Object,
            new Mock<global::Domain.Interfaces.IRepository<global::Domain.Entities.TournamentPlayer>>().Object,
            new Mock<ITournamentLifecycleService>().Object
        );
    }

    [Fact]
    public async Task RegisterTeamAsync_ShouldThrowConflict_WhenDeadlinePassed()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var tournament = new Tournament
        {
            Id = tournamentId,
            RegistrationDeadline = DateTime.UtcNow.AddHours(-1),
            AllowLateRegistration = false
        };

        _tournamentRepoMock.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>()))
            .ReturnsAsync(tournament);

        var request = new RegisterTeamRequest { TeamId = Guid.NewGuid() };

        // Act
        var act = () => _service.RegisterTeamAsync(tournamentId, request, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("انتهى موعد التسجيل في البطولة.");
    }

    [Fact]
    public async Task RegisterTeamAsync_ShouldThrowConflict_WhenTournamentFull()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var tournament = new Tournament
        {
            Id = tournamentId,
            RegistrationDeadline = DateTime.UtcNow.AddDays(1),
            MaxTeams = 2,
            Registrations = new List<TeamRegistration> { new(), new() }
        };

        _tournamentRepoMock.Setup(r => r.GetByIdAsync(tournamentId, It.IsAny<string[]>()))
            .ReturnsAsync(tournament);

        var request = new RegisterTeamRequest { TeamId = Guid.NewGuid() };

        // Act
        var act = () => _service.RegisterTeamAsync(tournamentId, request, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("اكتمل عدد الفرق في البطولة.");
    }
}
