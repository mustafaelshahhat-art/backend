using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Data;
using IntegrationTests.Base;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.EndpointTests;

[Collection("IntegrationTests")]
public class AuthEndpointTests
{
    private readonly IntegrationTestFactory _factory;

    public AuthEndpointTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_WithValidData_ShouldReturn201()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var formContent = new MultipartFormDataContent
        {
            { new StringContent($"register-{Guid.NewGuid():N}@test.com"), "Email" },
            { new StringContent("StrongPass123!"), "Password" },
            { new StringContent("Test Player"), "Name" },
            { new StringContent("0"), "Role" } // Player
        };

        // Act
        var response = await client.PostAsync("api/v1/auth/register", formContent);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnConflictOrBadRequest()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@test.com";

        var firstForm = new MultipartFormDataContent
        {
            { new StringContent(email), "Email" },
            { new StringContent("StrongPass123!"), "Password" },
            { new StringContent("First User"), "Name" },
            { new StringContent("0"), "Role" }
        };
        var firstResponse = await client.PostAsync("api/v1/auth/register", firstForm);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondForm = new MultipartFormDataContent
        {
            { new StringContent(email), "Email" },
            { new StringContent("StrongPass123!"), "Password" },
            { new StringContent("Second User"), "Name" },
            { new StringContent("0"), "Role" }
        };

        // Act
        var response = await client.PostAsync("api/v1/auth/register", secondForm);

        // Assert — typically 409 Conflict or 400 BadRequest for duplicate email
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturn200WithToken()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var client = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@test.com";

        // Register first
        var registerForm = new MultipartFormDataContent
        {
            { new StringContent(email), "Email" },
            { new StringContent("StrongPass123!"), "Password" },
            { new StringContent("Login Test User"), "Name" },
            { new StringContent("0"), "Role" }
        };
        var regResponse = await client.PostAsync("api/v1/auth/register", registerForm);
        regResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act
        var loginResponse = await client.PostAsJsonAsync("api/v1/auth/login", new
        {
            Email = email,
            Password = "StrongPass123!"
        });

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturn401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("api/v1/auth/login", new
        {
            Email = "nonexistent@test.com",
            Password = "WrongPassword123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturn401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act — TeamsController requires auth
        var response = await client.GetAsync("api/v1/teams");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminEndpoint_WithPlayerToken_ShouldReturn403()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Regular Player",
            email: $"player-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);

        // Act — RequireAdmin policy (e.g., disable a team)
        var fakeTeamId = Guid.NewGuid();
        var response = await client.PostAsync($"api/v1/teams/{fakeTeamId}/disable", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Logout_WithValidToken_ShouldReturn204()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Logout User",
            email: $"logout-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);

        // Act
        var response = await client.PostAsync("api/v1/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Lightweight DTO for deserializing auth responses in tests.
    /// </summary>
    private class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
