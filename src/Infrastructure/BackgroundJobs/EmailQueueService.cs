using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// PERF-FIX B4: Channel-based email queue + background processor.
/// Replaces Task.Run fire-and-forget pattern in AuthService.
/// Benefits:
///   - No scope leaks (proper DI scope per batch)
///   - No unobserved exceptions
///   - Back-pressure via bounded channel (1000 pending max)
///   - Graceful shutdown via stoppingToken
/// </summary>
public sealed class EmailQueueService : BackgroundService, IEmailQueueService
{
    private readonly Channel<EmailMessage> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailQueueService> _logger;

    public EmailQueueService(IServiceScopeFactory scopeFactory, ILogger<EmailQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        return _channel.Writer.TryWrite(new EmailMessage(toEmail, subject, body))
            ? ValueTask.CompletedTask
            : _channel.Writer.WriteAsync(new EmailMessage(toEmail, subject, body), ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email background queue started.");

        await foreach (var email in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailSvc = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await emailSvc.SendEmailAsync(email.To, email.Subject, email.Body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", email.To);
            }
        }
    }

    private sealed record EmailMessage(string To, string Subject, string Body);
}
