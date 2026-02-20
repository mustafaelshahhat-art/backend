using Application.Common.Interfaces;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Unified notification dispatcher that wraps INotificationService (persistent DB)
/// and IRealTimeNotifier (SignalR real-time push).
///
/// Replaces direct injection of INotificationService + IRealTimeNotifier
/// in command/query handlers. Used by domain event handlers to fire side-effects.
///
/// See: EXECUTION_PLAN §3.7, EXECUTION_BLUEPRINT §3.1 row 1
/// </summary>
public class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationService _notificationService;
    private readonly IRealTimeNotifier _realTimeNotifier;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        INotificationService notificationService,
        IRealTimeNotifier realTimeNotifier,
        ILogger<NotificationDispatcher> logger)
    {
        _notificationService = notificationService;
        _realTimeNotifier = realTimeNotifier;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotifyUserAsync(
        Guid userId, string title, string message,
        NotificationCategory category, Guid? entityId,
        string? entityType, CancellationToken ct)
    {
        try
        {
            // 1. Persist notification to DB + push via SignalR (INotificationService handles both)
            await _notificationService.SendNotificationAsync(
                userId, title, message, category,
                entityId: entityId, entityType: entityType, ct: ct);
        }
        catch (Exception ex)
        {
            // Event handlers must be resilient — log but don't throw
            _logger.LogError(ex,
                "Failed to dispatch notification to user {UserId}: {Title}",
                userId, title);
        }
    }

    /// <inheritdoc />
    public async Task NotifyUserByTemplateAsync(
        Guid userId, string templateKey,
        Dictionary<string, string>? parameters,
        Guid? entityId, string? entityType, CancellationToken ct)
    {
        try
        {
            await _notificationService.SendNotificationByTemplateAsync(
                userId, templateKey, parameters,
                entityId: entityId, entityType: entityType, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to dispatch template notification to user {UserId}: {TemplateKey}",
                userId, templateKey);
        }
    }

    /// <inheritdoc />
    public async Task NotifyRoleAsync(
        UserRole role, string title, string message,
        NotificationCategory category, CancellationToken ct)
    {
        try
        {
            await _notificationService.SendNotificationToRoleAsync(
                role, title, message, category, ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to dispatch role notification to {Role}: {Title}",
                role, title);
        }
    }
}
