using Domain.Entities;
using Domain.Events;
using FluentAssertions;
using IntegrationTests.Base;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class OutboxAtomicityTests
{
    private readonly IntegrationTestFactory _factory;

    public OutboxAtomicityTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Outbox_ShouldPersistWithDomainWrite()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Outbox Atomicity Test User",
            Email = $"outbox-atomicity-{Guid.NewGuid()}@test.com",
            PasswordHash = "hashed"
        };

        // Add a domain event to the entity — this should be captured and persisted
        // as an OutboxMessage atomically in the same SaveChangesAsync call.
        var domainEvent = new TournamentRegistrationApprovedEvent(
            tournamentId: Guid.NewGuid(),
            teamId: Guid.NewGuid(),
            captainUserId: user.Id,
            tournamentName: "Test Tournament",
            teamName: "Test Team"
        );
        user.AddDomainEvent(domainEvent);

        // Act
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // Assert — User was persisted
        var savedUser = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        savedUser.Should().NotBeNull();

        // Assert — OutboxMessage was created atomically in the same transaction
        var outboxMessage = await context.OutboxMessages
            .FirstOrDefaultAsync(m => m.Type == nameof(TournamentRegistrationApprovedEvent)
                                   && m.Payload.Contains(user.Id.ToString()));

        outboxMessage.Should().NotBeNull("an OutboxMessage should be written atomically with the entity");
        outboxMessage!.Status.Should().Be(OutboxMessageStatus.Pending);
        outboxMessage.RetryCount.Should().Be(0);
        outboxMessage.Payload.Should().Contain(domainEvent.TournamentName);

        // Assert — Domain events cleared from entity after save
        savedUser!.DomainEvents.Should().BeEmpty("domain events should be cleared after SaveChangesAsync");
    }
}
