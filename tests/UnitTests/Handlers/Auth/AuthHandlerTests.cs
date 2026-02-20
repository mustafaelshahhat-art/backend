using System.Linq.Expressions;
using Application.DTOs.Auth;
using Application.DTOs.Users;
using Application.Features.Auth.Commands.Login;
using Application.Features.Auth.Commands.Register;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using FluentAssertions;
using Moq;
using Shared.Exceptions;
using Xunit;

namespace UnitTests.Handlers.Auth;

/// <summary>
/// Phase 3 gate: Auth handler tests.
/// Handlers have inlined logic — tests validate direct behavior.
/// </summary>
public class AuthHandlerTests
{
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IAuthUserResolverService> _authUserResolver = new();
    private readonly Mock<IAuthTokenService> _authToken = new();
    private readonly Mock<ISystemSettingsService> _systemSettingsService = new();
    private readonly Mock<IOtpService> _otpService = new();
    private readonly Mock<IEmailQueueService> _emailQueue = new();

    private LoginCommandHandler CreateLoginHandler() => new(
        _userRepo.Object, _authUserResolver.Object, _authToken.Object,
        _systemSettingsService.Object);

    private RegisterCommandHandler CreateRegisterHandler() => new(
        _userRepo.Object, _authUserResolver.Object, _authToken.Object,
        _otpService.Object, _emailQueue.Object);

    #region Login

    [Fact]
    public async Task Login_ValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com", Name = "Test User",
            PasswordHash = "hashed", IsEmailVerified = true,
            Status = UserStatus.Active, Role = UserRole.Player
        };
        var userDto = new UserDto { Id = user.Id, Name = user.Name, Email = user.Email };

        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });
        _authToken.Setup(x => x.VerifyPassword("password123", "hashed")).Returns(true);
        _systemSettingsService.Setup(s => s.IsMaintenanceModeEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _authToken.Setup(x => x.GenerateToken(It.IsAny<User>())).Returns("jwt-token");
        _authToken.Setup(x => x.GenerateRefreshToken()).Returns("refresh-token");
        _authUserResolver.Setup(x => x.ResolveUserWithTeamAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userDto);

        var handler = CreateLoginHandler();
        var command = new LoginCommand(new LoginRequest { Email = "test@test.com", Password = "password123" });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().Be("jwt-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.User.Should().NotBeNull();
        result.User!.Name.Should().Be("Test User");
        result.User.Email.Should().Be("test@test.com");
    }

    [Fact]
    public async Task Login_InvalidPassword_ThrowsBadRequest()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com",
            PasswordHash = "hashed", IsEmailVerified = true,
            Status = UserStatus.Active, Role = UserRole.Player
        };

        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });
        _authToken.Setup(x => x.VerifyPassword("wrong", "hashed")).Returns(false);

        var handler = CreateLoginHandler();
        var command = new LoginCommand(new LoginRequest { Email = "test@test.com", Password = "wrong" });

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task Login_UnverifiedEmail_ThrowsEmailNotVerified()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com",
            PasswordHash = "hashed", IsEmailVerified = false,
            Status = UserStatus.Active, Role = UserRole.Player
        };

        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });
        _authToken.Setup(x => x.VerifyPassword("password123", "hashed")).Returns(true);
        _systemSettingsService.Setup(s => s.IsMaintenanceModeEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateLoginHandler();
        var command = new LoginCommand(new LoginRequest { Email = "test@test.com", Password = "password123" });

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<EmailNotVerifiedException>();
    }

    [Fact]
    public async Task Login_SuspendedUser_ThrowsForbidden()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "test@test.com",
            PasswordHash = "hashed", IsEmailVerified = true,
            Status = UserStatus.Suspended, Role = UserRole.Player
        };

        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { user });
        _authToken.Setup(x => x.VerifyPassword("password123", "hashed")).Returns(true);
        _systemSettingsService.Setup(s => s.IsMaintenanceModeEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = CreateLoginHandler();
        var command = new LoginCommand(new LoginRequest { Email = "test@test.com", Password = "password123" });

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region Register

    [Fact]
    public async Task Register_ValidRequest_ReturnsAuthResponse()
    {
        // Arrange
        var userDto = new UserDto { Id = Guid.NewGuid(), Name = "New User", Email = "new@test.com" };

        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<User>());
        _authToken.Setup(x => x.HashPassword("password123")).Returns("hashed-pw");
        _authToken.Setup(x => x.GenerateToken(It.IsAny<User>())).Returns("jwt-token");
        _authToken.Setup(x => x.GenerateRefreshToken()).Returns("refresh-token");
        _authUserResolver.Setup(x => x.ResolveUserWithTeamAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userDto);
        _otpService.Setup(o => o.GenerateOtpAsync(It.IsAny<Guid>(), "EMAIL_VERIFY", It.IsAny<CancellationToken>()))
            .ReturnsAsync("123456");

        var handler = CreateRegisterHandler();
        var command = new RegisterCommand(new RegisterRequest
        {
            Email = "new@test.com", Password = "password123", Name = "New User"
        });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Token.Should().Be("jwt-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.User.Should().NotBeNull();
        _userRepo.Verify(r => r.AddAsync(
            It.Is<User>(u => u.Email == "new@test.com" && u.Role == UserRole.Player),
            It.IsAny<CancellationToken>()), Times.Once);
        _emailQueue.Verify(e => e.EnqueueAsync(
            "new@test.com", It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ThrowsConflict()
    {
        // Arrange
        var existingUser = new User { Id = Guid.NewGuid(), Email = "existing@test.com" };

        _userRepo.Setup(r => r.FindAsync(
                It.IsAny<Expression<Func<User, bool>>>(),
                true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User> { existingUser });

        var handler = CreateRegisterHandler();
        var command = new RegisterCommand(new RegisterRequest
        {
            Email = "existing@test.com", Password = "password123", Name = "Test"
        });

        // Act & Assert
        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    #endregion
}
