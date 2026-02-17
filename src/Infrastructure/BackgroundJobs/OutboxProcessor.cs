using Domain.Entities;
using Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;

namespace Infrastructure.BackgroundJobs;

public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly IDomainEventTypeCache _typeCache;

    private const int BatchSize = 20;
    private const int MaxRetries = 3;

    // Adaptive polling: fast when busy, slow when idle
    private static readonly TimeSpan MinDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
    private TimeSpan _currentDelay = TimeSpan.FromSeconds(5);

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger, IDomainEventTypeCache typeCache)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _typeCache = typeCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessOutboxMessagesAsync(stoppingToken);

                // Adaptive polling: ramp down when idle, ramp up when busy
                if (processed > 0)
                {
                    _currentDelay = MinDelay; // Messages found — poll quickly
                }
                else
                {
                    // No messages — exponentially back off up to MaxDelay
                    _currentDelay = TimeSpan.FromMilliseconds(
                        Math.Min(_currentDelay.TotalMilliseconds * 2, MaxDelay.TotalMilliseconds));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing outbox messages.");
                _currentDelay = MaxDelay; // Error — back off fully
            }

            await Task.Delay(_currentDelay, stoppingToken);
        }
    }

    private async Task<int> ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var lockProvider = scope.ServiceProvider.GetRequiredService<IDistributedLock>();
        
        const string lockKey = "outbox_processor_lock";
        if (!await lockProvider.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), stoppingToken))
        {
            _logger.LogDebug("Outbox lock already held by another instance. Skipping.");
            return 0;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

            // PROD-AUDIT: Initial fetch of pending/failed messages that are due
            var messages = await dbContext.OutboxMessages
                .Where(m => m.Status == OutboxMessageStatus.Pending || m.Status == OutboxMessageStatus.Failed)
                .Where(m => m.RetryCount < MaxRetries)
                .Where(m => m.ScheduledAt <= DateTime.UtcNow)
                .OrderBy(m => m.OccurredOn)
                .Take(BatchSize)
                .ToListAsync(stoppingToken);

            if (!messages.Any()) return 0;

            // PROD-AUDIT: Atomic state transition to prevent double processing
            foreach (var message in messages)
            {
                message.Status = OutboxMessageStatus.Processing;
                message.UpdatedAt = DateTime.UtcNow;
            }

            try
            {
                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrency conflict detected while marking outbox messages. Skipping batch.");
                return 0;
            }

            foreach (var message in messages)
            {
                try
                {
                    var eventType = _typeCache.GetEventType(message.Type);
                    if (eventType == null) throw new Exception($"Event type {message.Type} not found.");

                    var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType);
                    if (domainEvent == null) throw new Exception($"Failed to deserialize event {message.Type}.");

                    await publisher.Publish(domainEvent, stoppingToken);

                    message.Status = OutboxMessageStatus.Processed;
                    message.ProcessedOn = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing outbox message {MessageId}", message.Id);
                    message.RetryCount++;
                    message.Error = ex.ToString();
                    
                    if (message.RetryCount >= MaxRetries)
                    {
                        message.Status = OutboxMessageStatus.DeadLetter;
                        message.DeadLetterReason = ex.Message;
                    }
                    else
                    {
                        message.Status = OutboxMessageStatus.Failed;
                        // Exponential backoff: 2^RetryCount * 30 seconds
                        var delaySeconds = Math.Pow(2, message.RetryCount) * 30;
                        message.ScheduledAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                    }
                }

                message.UpdatedAt = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            sw.Stop();
            _logger.LogInformation("Processed outbox batch of {Count} messages in {Duration}ms.", messages.Count, sw.ElapsedMilliseconds);

            // PROD-AUDIT: Alerting for high DeadLetter count
            var deadLetterCount = await dbContext.OutboxMessages
                .CountAsync(m => m.Status == OutboxMessageStatus.DeadLetter, stoppingToken);
            
            if (deadLetterCount > 50)
            {
                _logger.LogCritical("[OUTBOX_ALERT] High number of DeadLetter messages detected: {Count}. Manual intervention required.", deadLetterCount);
            }

            return messages.Count;
        }
        finally
        {
            await lockProvider.ReleaseLockAsync(lockKey, CancellationToken.None);
        }
    }
}
