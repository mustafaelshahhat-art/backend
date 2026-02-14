using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

public class IdempotencyCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyCleanupService> _logger;

    public IdempotencyCleanupService(IServiceScopeFactory scopeFactory, ILogger<IdempotencyCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredRequestsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during idempotency cleanup.");
            }

            // Run once every 6 hours
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task CleanupExpiredRequestsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-1);

        var expiredRequests = await dbContext.IdempotentRequests
            .Where(r => r.CreatedAt < cutoff && r.Status != Domain.Entities.IdempotencyStatus.InProgress)
            .Take(100)
            .ToListAsync(stoppingToken);

        if (expiredRequests.Any())
        {
            _logger.LogInformation("Cleaning up {Count} expired idempotency records.", expiredRequests.Count);
            dbContext.IdempotentRequests.RemoveRange(expiredRequests);
            await dbContext.SaveChangesAsync(stoppingToken);
        }
    }
}
