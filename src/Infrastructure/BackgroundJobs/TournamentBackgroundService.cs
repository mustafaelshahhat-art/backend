using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MediatR;
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
    }
}
