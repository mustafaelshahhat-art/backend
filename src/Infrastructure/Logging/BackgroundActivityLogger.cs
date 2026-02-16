using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Application.Common;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Logging;

public class BackgroundActivityLogger : BackgroundService, IBackgroundActivityLogger
{
    private readonly Channel<Activity> _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BackgroundActivityLogger> _logger;

    public BackgroundActivityLogger(IServiceProvider serviceProvider, ILogger<BackgroundActivityLogger> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _channel = Channel.CreateBounded<Activity>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    // ── Original (backward compat) ──

    public void LogActivity(string type, string message, Guid? userId = null, string? userName = null)
    {
        LogActivity(type, message, userId, userName, null, null, null, null, null);
    }

    public void LogActivityByTemplate(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null)
    {
        LogActivityByTemplate(code, placeholders, userId, userName, null, null, null, null, null);
    }

    // ── Enriched overloads ──

    public void LogActivity(string type, string message, Guid? userId, string? userName,
        string? actorRole, Guid? entityId, string? entityType, string? entityName, string? metadata)
    {
        // Auto-enrich severity + entityType from constants
        var severity = ActivitySeverity.Info;
        string? autoEntityType = null;
        if (ActivityConstants.Library.TryGetValue(type, out var meta))
        {
            severity = meta.Severity;
            autoEntityType = meta.EntityType;
        }

        var activity = new Activity
        {
            Type = type,
            Message = message,
            UserId = userId,
            UserName = userName,
            Severity = severity,
            ActorRole = actorRole,
            EntityType = entityType ?? autoEntityType,
            EntityId = entityId,
            EntityName = entityName,
            Metadata = metadata,
            CreatedAt = DateTime.UtcNow
        };
        _channel.Writer.TryWrite(activity);
    }

    public void LogActivityByTemplate(string code, Dictionary<string, string> placeholders, Guid? userId, string? userName,
        string? actorRole, Guid? entityId, string? entityType, string? entityName, string? metadata)
    {
        var localized = ActivityConstants.GetLocalized(code, placeholders);
        LogActivity(code, localized.Message, userId, userName, actorRole, entityId, entityType ?? localized.EntityType, entityName, metadata);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background Activity Logger started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var activity = await _channel.Reader.ReadAsync(stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IRepository<Activity>>();
                
                await repository.AddAsync(activity, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing background activity log.");
                await Task.Delay(1000, stoppingToken); // Backoff
            }
        }
    }
}
