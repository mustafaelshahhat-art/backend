using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using FluentAssertions;
using Infrastructure.BackgroundJobs;
using IntegrationTests.Base;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class OutboxProcessorTests
{
    private readonly IntegrationTestFactory _factory;

    public OutboxProcessorTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OutboxProcessor_ShouldProcessPendingMessages()
    {
        // Arrange — Insert pending OutboxMessages directly
        using var setupScope = _factory.Services.CreateScope();
        var setupContext = setupScope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        var domainEvent = new TournamentRegistrationApprovedEvent(
            tournamentId: Guid.NewGuid(),
            teamId: Guid.NewGuid(),
            captainUserId: Guid.NewGuid(),
            tournamentName: "Processor Test Tournament",
            teamName: "Processor Test Team"
        );

        var message1 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            Type = nameof(TournamentRegistrationApprovedEvent),
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            Status = OutboxMessageStatus.Pending,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1), // Due for processing
            RetryCount = 0
        };

        var message2 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            Type = nameof(TournamentRegistrationApprovedEvent),
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            Status = OutboxMessageStatus.Pending,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
            RetryCount = 0
        };

        setupContext.OutboxMessages.AddRange(message1, message2);
        await setupContext.SaveChangesAsync();

        // Arrange — Create OutboxProcessor with a mock IPublisher that succeeds
        var mockPublisher = new Mock<IPublisher>();
        mockPublisher
            .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Replace IPublisher in the service collection for the processor's scope
        var services = new ServiceCollection();
        // Copy the real scope factory's AppDbContext and IDistributedLock registrations
        services.AddScoped(_ =>
        {
            var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
        });
        services.AddScoped(_ =>
        {
            var scope = _factory.Services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        });
        services.AddSingleton(mockPublisher.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var logger = _factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger<OutboxProcessor>();
        var typeCache = _factory.Services.GetRequiredService<IDomainEventTypeCache>();

        var processor = new OutboxProcessor(scopeFactory, logger, typeCache);

        // Act — Run the processor with a short-lived cancellation token (one cycle)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            await processor.StartAsync(cts.Token);
            await Task.Delay(TimeSpan.FromSeconds(7), cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected — processor loop was cancelled
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
        }

        // Assert — Messages should now be Processed
        using var assertScope = _factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        var processedMsg1 = await assertContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == message1.Id);
        var processedMsg2 = await assertContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == message2.Id);

        processedMsg1.Should().NotBeNull();
        processedMsg1!.Status.Should().Be(OutboxMessageStatus.Processed);
        processedMsg1.ProcessedOn.Should().NotBeNull();

        processedMsg2.Should().NotBeNull();
        processedMsg2!.Status.Should().Be(OutboxMessageStatus.Processed);
        processedMsg2.ProcessedOn.Should().NotBeNull();

        // Assert — Publisher was called for each message
        mockPublisher.Verify(
            p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(2)
        );
    }
}
