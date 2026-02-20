using System.Net;
using System.Net.Http.Json;
using Domain.Enums;
using FluentAssertions;
using IntegrationTests.Base;
using Xunit;

namespace IntegrationTests.EndpointTests;

[Collection("IntegrationTests")]
public class TournamentEndpointTests
{
    private readonly IntegrationTestFactory _factory;

    public TournamentEndpointTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAll_Anonymous_ShouldReturn200()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("api/v1/tournaments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetById_NonExistent_ShouldReturn404()
    {
        // Arrange
        var client = _factory.CreateClient();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"api/v1/tournaments/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithCreatorToken_ShouldReturn201()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Tournament Creator",
            email: $"creator-{Guid.NewGuid():N}@test.com",
            role: UserRole.TournamentCreator);

        var request = new
        {
            Name = "Integration Test Tournament",
            Description = "Created via integration test",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(60),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            EntryFee = 100m,
            MaxTeams = 16,
            Location = "Test Stadium",
            Rules = "Standard rules",
            Prizes = "Trophy",
            Format = (int)TournamentFormat.KnockoutOnly,
            MatchType = (int)TournamentLegType.SingleLeg,
            NumberOfGroups = 0,
            SchedulingMode = (int)SchedulingMode.Manual,
            LateRegistrationMode = (int)LateRegistrationMode.None
        };

        // Act
        var response = await client.PostAsJsonAsync("api/v1/tournaments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<TournamentResponseDto>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeEmpty();
        body.Name.Should().Be("Integration Test Tournament");
    }

    [Fact]
    public async Task Create_WithoutAuth_ShouldReturn401()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new
        {
            Name = "Unauthorized Tournament",
            Description = "Should fail",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(60),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            EntryFee = 50m,
            MaxTeams = 8,
            Location = "Nowhere",
            Rules = "None",
            Prizes = "Nothing",
            Format = (int)TournamentFormat.KnockoutOnly,
            MatchType = (int)TournamentLegType.SingleLeg,
            NumberOfGroups = 0
        };

        // Act
        var response = await client.PostAsJsonAsync("api/v1/tournaments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WithPlayerToken_ShouldReturn403()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, _) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Regular Player",
            email: $"player-{Guid.NewGuid():N}@test.com",
            role: UserRole.Player);

        var request = new
        {
            Name = "Player Cannot Create Tournament",
            Description = "Should fail",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(60),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            EntryFee = 50m,
            MaxTeams = 8,
            Location = "Test",
            Rules = "Test",
            Prizes = "Test",
            Format = (int)TournamentFormat.KnockoutOnly,
            MatchType = (int)TournamentLegType.SingleLeg,
            NumberOfGroups = 0
        };

        // Act
        var response = await client.PostAsJsonAsync("api/v1/tournaments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_AsOwner_ShouldReturn204()
    {
        // Arrange
        await _factory.ResetDatabaseAsync();
        var (client, user) = await TestAuthHelper.CreateSeededAuthenticatedClientAsync(
            _factory,
            name: "Owner Creator",
            email: $"owner-{Guid.NewGuid():N}@test.com",
            role: UserRole.TournamentCreator);

        // Create a tournament first
        var createRequest = new
        {
            Name = "Tournament To Delete",
            Description = "Will be deleted",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(60),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            EntryFee = 0m,
            MaxTeams = 8,
            Location = "Test",
            Rules = "Test",
            Prizes = "Test",
            Format = (int)TournamentFormat.KnockoutOnly,
            MatchType = (int)TournamentLegType.SingleLeg,
            NumberOfGroups = 0,
            SchedulingMode = (int)SchedulingMode.Manual,
            LateRegistrationMode = (int)LateRegistrationMode.None
        };

        var createResponse = await client.PostAsJsonAsync("api/v1/tournaments", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<TournamentResponseDto>();

        // Act
        var deleteResponse = await client.DeleteAsync($"api/v1/tournaments/{created!.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetMatches_ForTournament_ShouldReturn200()
    {
        // Arrange
        var client = _factory.CreateClient();
        var nonExistentId = Guid.NewGuid();

        // Act — even for non-existent tournament, the endpoint returns 200 with empty list
        var response = await client.GetAsync($"api/v1/tournaments/{nonExistentId}/matches");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStandings_Anonymous_ShouldReturn200()
    {
        // Arrange
        var client = _factory.CreateClient();
        var tournamentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"api/v1/tournaments/{tournamentId}/standings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Lightweight DTO for deserializing tournament responses in tests.
    /// </summary>
    private class TournamentResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
