using System.Linq.Expressions;
using Application.DTOs.Users;
using Application.Features.Users.Commands.CreateAdmin;
using Application.Features.Users.Commands.SuspendUser;
using Application.Features.Users.Commands.ChangePassword;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;
using Shared.Exceptions;
using Xunit;

namespace UnitTests.Handlers.Users;

/// <summary>
/// Phase 3 gate: User handler tests.
/// Handlers have inlined logic — tests validate direct behavior.
/// </summary>
public class UserHandlerTests
{
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IRealTimeNotifier> _realTimeNotifier = new();
    private readonly Mock<ISystemSettingsService> _systemSettingsService = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IUserCacheService> _userCacheService = new();

    private CreateAdminCommandHandler CreateAdminHandler() => new(
        _userRepo.Object, _passwordHasher.Object, _mapper.Object,
        _realTimeNotifier.Object, _systemSettingsService.Object);

    private SuspendUserCommandHandler CreateSuspendHandler() => new(
        _userRepo.Object, _mapper.Object, _realTimeNotifier.Object,
        _notificationService.Object, _userCacheService.Object);

    private ChangePasswordCommandHandler CreateChangePasswordHandler() => new(
        _userRepo.Object, _passwordHasher.Object,
        _realTimeNotifier.Object, _notificationService.Object);

    #region CreateAdmin (delegating — critical security path)

    [Fact]
    public async Task CreateAdmin_ValidRequest_ReturnsUserDto()
    {
        // Arrange
        _systemSettingsService.Setup(s => s.IsMaintenanceModeEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());
        _passwordHasher.Setup(h => h.HashPassword("Admin123!")).Returns("hashed");
        var expectedDto = new UserDto { Name = "Admin", Email = "admin@test.com", Role = "Admin" };
        _mapper.Setup(m => m.Map<UserDto>(It.IsAny<User>())).Returns(expectedDto);

        var handler = CreateAdminHandler();
        var command = new CreateAdminCommand(
            new CreateAdminRequest { Name = "Admin", Email = "admin@test.com", Password = "Admin123!" },
            Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Role.Should().Be("Admin");
        _userRepo.Verify(r => r.AddAsync(
            It.Is<User>(u => u.Role == UserRole.Admin && u.Email == "admin@test.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAdmin_DuplicateEmail_ThrowsConflict()
    {
        // Arrange
        _systemSettingsService.Setup(s => s.IsMaintenanceModeEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var existingUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com" };
        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { existingUser });

        var handler = CreateAdminHandler();
        var command = new CreateAdminCommand(
            new CreateAdminRequest { Name = "Admin", Email = "admin@test.com", Password = "Admin123!" },
            Guid.NewGuid());

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    #endregion

    #region SuspendUser (delegating — token invalidation logic)

    [Fact]
    public async Task SuspendUser_IncrementTokenVersion_InvalidatesSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, Email = "user@test.com", Name = "User",
            Status = UserStatus.Active, Role = UserRole.Player,
            TokenVersion = 1
        };

        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mapper.Setup(m => m.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto { Id = userId });

        var handler = CreateSuspendHandler();
        var command = new SuspendUserCommand(userId);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.Status.Should().Be(UserStatus.Suspended);
        user.TokenVersion.Should().Be(2);
        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.Status == UserStatus.Suspended && u.TokenVersion == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuspendUser_LastAdmin_ThrowsBadRequest()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var admin = new User
        {
            Id = adminId, Email = "admin@test.com", Name = "Admin",
            Status = UserStatus.Active, Role = UserRole.Admin,
            IsEmailVerified = true
        };

        _userRepo.Setup(r => r.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(admin);
        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { admin });

        var handler = CreateSuspendHandler();
        var command = new SuspendUserCommand(adminId);

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BadRequestException>();
    }

    #endregion

    #region ChangePassword (delegating — security path)

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ThrowsBadRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, Email = "user@test.com",
            PasswordHash = "old-hash", TokenVersion = 1
        };

        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword("wrong-password", "old-hash")).Returns(false);

        var handler = CreateChangePasswordHandler();
        var command = new ChangePasswordCommand(userId, "wrong-password", "new-password123");

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task ChangePassword_Valid_IncrementsTokenVersion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId, Email = "user@test.com",
            PasswordHash = "old-hash", TokenVersion = 1
        };

        _userRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword("current-pwd", "old-hash")).Returns(true);
        _passwordHasher.Setup(h => h.HashPassword("new-password123")).Returns("new-hash");

        var handler = CreateChangePasswordHandler();
        var command = new ChangePasswordCommand(userId, "current-pwd", "new-password123");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        user.PasswordHash.Should().Be("new-hash");
        user.TokenVersion.Should().Be(2);
        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.PasswordHash == "new-hash" && u.TokenVersion == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
