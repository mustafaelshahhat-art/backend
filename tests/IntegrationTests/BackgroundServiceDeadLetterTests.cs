using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.BackgroundJobs;
using IntegrationTests.Base;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntegrationTests;

[Collection("IntegrationTests")]
public class BackgroundServiceDeadLetterTests
{
    private readonly IntegrationTestFactory _factory;

    public BackgroundServiceDeadLetterTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task BackgroundService_DeadLetterTest()
    {
        // Arrange — Insert a message with an INVALID event type that will always fail deserialization
        using var setupScope = _factory.Services.CreateScope();
        var setupContext = setupScope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        var poisonMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = DateTime.UtcNow,
            Type = "NonExistentEvent", // This type cannot be resolved by DomainEventTypeCache
            Payload = "{}",
            Status = OutboxMessageStatus.Pending,
            ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
            RetryCount = 0
        };

        setupContext.OutboxMessages.Add(poisonMessage);
        await setupContext.SaveChangesAsync();

        // Arrange — Create processor (IPublisher should never be called for this message)
        var mockPublisher = new Mock<IPublisher>();

        // MaxRetries in OutboxProcessor is 3, so we need 3 processing cycles
        // Each cycle: the processor picks up the Failed message, retries, fails again,
        // increments RetryCount, sets exponential backoff ScheduledAt.
        // We must reset ScheduledAt after each cycle so it's picked up again.

        for (int cycle = 0; cycle < 3; cycle++)
        {
            // Ensure the message is due for processing
            using var resetScope = _factory.Services.CreateScope();
            var resetContext = resetScope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();
            var msg = await resetContext.OutboxMessages.FirstAsync(m => m.Id == poisonMessage.Id);
            msg.ScheduledAt = DateTime.UtcNow.AddMinutes(-1);
            await resetContext.SaveChangesAsync();

            // Build a fresh processor for each cycle
            var services = new ServiceCollection();
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

            // Act — Run one cycle
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try
            {
                await processor.StartAsync(cts.Token);
                await Task.Delay(TimeSpan.FromSeconds(7), cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }

        // Assert — After 3 failures, the message should be DeadLettered
        using var assertScope = _factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        var deadLetteredMsg = await assertContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == poisonMessage.Id);

        deadLetteredMsg.Should().NotBeNull();
        deadLetteredMsg!.Status.Should().Be(OutboxMessageStatus.DeadLetter,
            "a message that fails 3 times should be moved to DeadLetter");
        deadLetteredMsg.RetryCount.Should().Be(3);
        deadLetteredMsg.DeadLetterReason.Should().NotBeNullOrEmpty(
            "the reason for dead-lettering should be captured");
        deadLetteredMsg.Error.Should().NotBeNullOrEmpty(
            "the error details should be preserved");

        // Publisher should never have been called for this poison message
        mockPublisher.Verify(
            p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a poison message should never reach the publisher"
        );
    }
}
