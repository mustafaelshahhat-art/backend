using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using FluentAssertions;
using IntegrationTests.Base;
using Xunit;

namespace IntegrationTests.EndpointTests;

[Collection("IntegrationTests")]
public class TeamEndpointTests
{
    private readonly IntegrationTestFactory _factory;

    public TeamEndpointTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_Authenticated_ShouldReturn200()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Team Viewer",
            email: $"viewer-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);

        // Act
        var response = await client.GetAsync("api/v1/teams");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ShouldReturn401()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("api/v1/teams");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithAuth_ShouldReturn201()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Team Captain",
            email: $"captain-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);

        var request = new
        {
            Name = $"Test FC {Guid.NewGuid():N}",
            Founded = "2024",
            City = "Cairo"
        };

        // Act
        var response = await client.PostAsJsonAsync("api/v1/teams", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<TeamResponseDto>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Name.Should().StartWith("Test FC");
    }

    [Fact]
    public async Task Create_WithoutAuth_ShouldReturn401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            Name = "Unauthorized Team",
            Founded = "2024",
            City = "Cairo"
        };

        // Act
        var response = await client.PostAsJsonAsync("api/v1/teams", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldReturn404()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Lookup User",
            email: $"lookup-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"api/v1/teams/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_AsNonCaptain_ShouldReturn403()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();

        // Seed two users: one creates the team, the other tries to delete it
        var ownerUser = await TestAuthHelper.SeedUserAsync(
            _factory,
            name: "Team Owner",
            email: $"owner-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);

        var ownerClient = TestAuthHelper.CreateAuthenticatedClient(_factory, ownerUser.Id, ownerUser.Email, UserRole.Player.ToString());

        var createRequest = new
        {
            Name = $"Owner Team {Guid.NewGuid():N}",
            Founded = "2024",
            City = "Cairo"
        };
        var createResponse = await ownerClient.PostAsJsonAsync("api/v1/teams", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdTeam = await createResponse.Content.ReadFromJsonAsync<TeamResponseDto>();

        // Create a different user who is NOT the captain
        var (otherClient, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Other Player",
            email: $"other-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);

        // Act
        var deleteResponse = await otherClient.DeleteAsync($"api/v1/teams/{createdTeam!.Id}");

        // Assert — should be 403 Forbidden (RequireTeamCaptain policy)
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetTeamPlayers_Authenticated_ShouldReturn200()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, user) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Player Checker",
            email: $"checker-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);

        // Create a team first
        var createRequest = new
        {
            Name = $"Players Team {Guid.NewGuid():N}",
            Founded = "2024",
            City = "Alexandria"
        };
        var createResponse = await client.PostAsJsonAsync("api/v1/teams", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var team = await createResponse.Content.ReadFromJsonAsync<TeamResponseDto>();

        // Act
        var response = await client.GetAsync($"api/v1/teams/{team!.Id}/players");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Disable_AsAdmin_ShouldReturn200()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();

        // Create team with a player
        var playerUser = await TestAuthHelper.SeedUserAsync(
            _factory,
            name: "Player Owner",
            email: $"powner-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);
        var playerClient = TestAuthHelper.CreateAuthenticatedClient(_factory, playerUser.Id, playerUser.Email, UserRole.Player.ToString());

        var createResponse = await playerClient.PostAsJsonAsync("api/v1/teams", new
        {
            Name = $"Team To Disable {Guid.NewGuid():N}",
            Founded = "2024",
            City = "Giza"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var team = await createResponse.Content.ReadFromJsonAsync<TeamResponseDto>();

        // Admin client
        var (adminClient, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Admin User",
            email: $"admin-{Guid.NewGuid():N}@test.com",
            role: UserRole.Admin);

        // Act
        var response = await adminClient.PostAsync($"api/v1/teams/{team!.Id}/disable", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Lightweight DTO for deserializing team responses in tests.
    /// </summary>
    private class TeamResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
