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
public class OutboxConcurrencyTests
{
    private readonly IntegrationTestFactory _factory;

    public OutboxConcurrencyTests(IntegrationTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Outbox_ShouldNotDuplicateUnderParallelExecution()
    {
        // Arrange — Insert a batch of pending messages
        using var setupScope = _factory.Services.CreateScope();
        var setupContext = setupScope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        var domainEvent = new TournamentRegistrationApprovedEvent(
            tournamentId: Guid.NewGuid(),
            teamId: Guid.NewGuid(),
            captainUserId: Guid.NewGuid(),
            tournamentName: "Concurrency Test",
            teamName: "Concurrency Team"
        );

        var messageIds = new List<Guid>();
        for (int i = 0; i < 5; i++)
        {
            var msg = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OccurredOn = DateTime.UtcNow,
                Type = nameof(TournamentRegistrationApprovedEvent),
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                Status = OutboxMessageStatus.Pending,
                ScheduledAt = DateTime.UtcNow.AddMinutes(-1),
                RetryCount = 0
            };
            messageIds.Add(msg.Id);
            setupContext.OutboxMessages.Add(msg);
        }
        await setupContext.SaveChangesAsync();

        // Track how many times Publish is called across both processors
        var publishCount = 0;

        // Create two independent processors that will race
        OutboxProcessor CreateProcessor()
        {
            var mockPublisher = new Mock<IPublisher>();
            mockPublisher
                .Setup(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Interlocked.Increment(ref publishCount);
                    return Task.CompletedTask;
                });

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

            return new OutboxProcessor(scopeFactory, logger, typeCache);
        }

        var processor1 = CreateProcessor();
        var processor2 = CreateProcessor();

        // Act — Both processors race to process the same messages
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        var task1 = Task.Run(async () =>
        {
            await processor1.StartAsync(cts.Token);
            try { await Task.Delay(TimeSpan.FromSeconds(9), cts.Token); }
            catch (OperationCanceledException) { }
            await processor1.StopAsync(CancellationToken.None);
        });

        var task2 = Task.Run(async () =>
        {
            await processor2.StartAsync(cts.Token);
            try { await Task.Delay(TimeSpan.FromSeconds(9), cts.Token); }
            catch (OperationCanceledException) { }
            await processor2.StopAsync(CancellationToken.None);
        });

        await Task.WhenAll(task1, task2);

        // Assert — Each message should be processed exactly once
        using var assertScope = _factory.Services.CreateScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<Infrastructure.Data.AppDbContext>();

        foreach (var id in messageIds)
        {
            var msg = await assertContext.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id);
            msg.Should().NotBeNull();
            msg!.Status.Should().Be(OutboxMessageStatus.Processed,
                $"message {id} should be processed exactly once");
        }

        // The total number of publishes should equal the number of messages (no duplicates)
        publishCount.Should().Be(5, "each message should be published exactly once, no duplicates");
    }
}
