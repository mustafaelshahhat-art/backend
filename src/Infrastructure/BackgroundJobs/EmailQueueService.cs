using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Channel-based email queue + background processor with retry durability.
/// Uses a bounded channel for back-pressure and retries failed sends
/// with exponential backoff up to 3 times before dead-lettering.
/// 
/// Durability: failed messages are retried in-process with backoff.
/// For full persistence across restarts, integrate with the existing
/// OutboxMessage table by enqueuing an EmailSendRequested domain event.
/// </summary>
public sealed class EmailQueueService : BackgroundService, IEmailQueueService
{
    private readonly Channel<EmailMessage> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailQueueService> _logger;
    private const int MaxRetries = 3;

    public EmailQueueService(IServiceScopeFactory scopeFactory, ILogger<EmailQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _channel = Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait, // Block producers instead of dropping
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        var msg = new EmailMessage(toEmail, subject, body, RetryCount: 0);
        return _channel.Writer.TryWrite(msg)
            ? ValueTask.CompletedTask
            : _channel.Writer.WriteAsync(msg, ct);
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
                _logger.LogError(ex, "Failed to send email to {Email} (attempt {Attempt}/{Max})",
                    email.To, email.RetryCount + 1, MaxRetries);

                if (email.RetryCount < MaxRetries)
                {
                    // Exponential backoff: 1s, 2s, 4s
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, email.RetryCount));
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(delay, stoppingToken);
                        var retryMsg = email with { RetryCount = email.RetryCount + 1 };
                        await _channel.Writer.WriteAsync(retryMsg, stoppingToken);
                    }, stoppingToken);
                }
                else
                {
                    _logger.LogCritical("Email to {Email} dead-lettered after {Max} retries. Subject: {Subject}",
                        email.To, MaxRetries, email.Subject);
                }
            }
        }
    }

    private sealed record EmailMessage(string To, string Subject, string Body, int RetryCount);
}
