using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
using Application.Interfaces;
using Application.Features.Tournaments.Commands.ProcessAutomatedEvents;

namespace Infrastructure.BackgroundJobs;

public class TournamentBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TournamentBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    public TournamentBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TournamentBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tournament Background Service is starting.");

        // Run immediately on start
        await DoWorkAsync(stoppingToken);

        using var timer = new PeriodicTimer(_checkInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DoWorkAsync(stoppingToken);
        }
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        const string lockKey = "tournament_events_processor";
        var distributedLock = _scopeFactory.CreateScope().ServiceProvider.GetRequiredService<IDistributedLock>();
        try
        {
            // TTL of 6 minutes (higher than 5 min interval to prevent overlaps on drift, but short enough to recover on crash)
            if (!await distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(6)))
            {
                _logger.LogWarning("FAILED to acquire distributed lock for {LockKey}. Skipping this cycle to prevent overlap.", lockKey);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error acquiring distributed lock for {LockKey}. Skipping cycle for safety.", lockKey);
            return;
        }

        try
        {
            _logger.LogInformation("Processing automated tournament events...");
            
            using (var scope = _scopeFactory.CreateScope())
            {
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(new ProcessAutomatedEventsCommand(), stoppingToken);
            }
            
            _logger.LogInformation("Automated tournament events processed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while processing automated tournament events.");
        }
        finally
        {
            await distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
