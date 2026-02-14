using Application.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Resilience;

namespace Infrastructure.Services;

public class ResilientEmailService : IEmailService
{
    private readonly IEmailService _innerService;
    private readonly ILogger<ResilientEmailService> _logger;
    private readonly IAsyncPolicy _resiliencePolicy;

    public ResilientEmailService(IEmailService innerService, ILogger<ResilientEmailService> logger)
    {
        _innerService = innerService;
        _logger = logger;
        _resiliencePolicy = EmailPolicies.GetResiliencePolicy(logger);
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        await _resiliencePolicy.ExecuteAsync(async (token) => 
        {
            await _innerService.SendEmailAsync(to, subject, body, token);
        }, ct);
    }
}
