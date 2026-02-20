using Application.Features.Tournaments.Commands.CreateTournament;
using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Moq;
using Shared.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Application.Common.Interfaces;
using Application.Interfaces;
using Domain.Interfaces;
using AutoMapper;
using System.Collections.Generic;

namespace UnitTests;

public class TournamentCorrectionTests
{
    private readonly Mock<IRepository<Tournament>> _repoMock = new();
    private readonly Mock<IActivityLogger> _activityLoggerMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IRealTimeNotifier> _notifierMock = new();

    private CreateTournamentCommandHandler CreateHandler()
    {
        return new CreateTournamentCommandHandler(
            _repoMock.Object,
            _mapperMock.Object,
            _activityLoggerMock.Object,
            _notifierMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldSetStatusToDraft()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new CreateTournamentRequest 
        { 
            Name = "New Tournament", 
            Mode = TournamentMode.LeagueSingle,
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            RegistrationDeadline = DateTime.UtcNow.AddDays(0.5)
        };
        var command = new CreateTournamentCommand(request, Guid.NewGuid());

        Tournament? captured = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Tournament>(), It.IsAny<CancellationToken>()))
                 .Callback<Tournament, CancellationToken>((t, ct) => captured = t);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(TournamentStatus.Draft);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenNameAlreadyExists()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new CreateTournamentRequest 
        { 
            Name = "Existing Name",
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            RegistrationDeadline = DateTime.UtcNow.AddDays(0.5)
        };
        var command = new CreateTournamentCommand(request, Guid.NewGuid());

        _repoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Tournament, bool>>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(true);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<Shared.Exceptions.BadRequestException>()
                 .WithMessage("اسم البطولة مستخدم بالفعل. يرجى اختيار اسم آخر.");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenKnockoutTeamsNotPowerOf2()
    {
        // Arrange
        var handler = CreateHandler();
        var request = new CreateTournamentRequest 
        { 
            Name = "Knockout Test", 
            Mode = TournamentMode.KnockoutSingle, 
            MaxTeams = 10, // Not power of 2
            StartDate = DateTime.UtcNow.AddDays(1),
            EndDate = DateTime.UtcNow.AddDays(2),
            RegistrationDeadline = DateTime.UtcNow.AddDays(0.5)
        };
        var command = new CreateTournamentCommand(request, Guid.NewGuid());

        _repoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Tournament, bool>>>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(false);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<Shared.Exceptions.BadRequestException>()
                 .WithMessage("*يجب أن يكون عدد الفرق من مضاعفات الرقم 2*");
    }
}
