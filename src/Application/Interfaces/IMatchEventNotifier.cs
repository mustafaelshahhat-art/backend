using Application.DTOs.Tournaments;
using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces;

/// <summary>
/// Aggregates real-time, analytics, and push notification concerns for match events.
/// Reduces constructor dependencies in match command handlers.
/// </summary>
public interface IMatchEventNotifier
{
    /// <summary>Maps match to DTO and sends real-time update</summary>
    Task SendMatchUpdateAsync(Match match, CancellationToken ct = default);

    /// <summary>Maps tournament to DTO and sends real-time update</summary>
    Task SendTournamentUpdateAsync(Tournament tournament, CancellationToken ct = default);

    /// <summary>Logs an activity via the analytics service</summary>
    Task LogActivityAsync(string templateCode, Dictionary<string, string> placeholders,
        Guid? userId = null, string? userName = null, CancellationToken ct = default);

    /// <summary>Sends a push notification via the notification service</summary>
    Task SendNotificationAsync(Guid userId, string title, string message,
        NotificationCategory category = NotificationCategory.System,
        NotificationType type = NotificationType.Info,
        CancellationToken ct = default);

    /// <summary>Handles post-lifecycle outcomes (groups finished, knockout started, tournament finalized)</summary>
    Task HandleLifecycleOutcomeAsync(TournamentLifecycleResult result, CancellationToken ct = default);
}
