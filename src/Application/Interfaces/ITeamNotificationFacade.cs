using Application.DTOs.Users;
using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Aggregates real-time, push notification, and mapping concerns for team member events.
/// Reduces constructor dependencies in team command handlers.
/// </summary>
public interface ITeamNotificationFacade
{
    /// <summary>Maps user to UserDto and sends real-time update</summary>
    Task SendUserUpdatedAsync(User user, CancellationToken ct = default);

    /// <summary>Sends a real-time user-created event with a pre-mapped DTO</summary>
    Task SendUserCreatedAsync(UserDto userDto, CancellationToken ct = default);

    /// <summary>Loads team snapshot with Players+Statistics, maps to TeamDto, sends real-time update</summary>
    Task SendTeamUpdatedAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>Sends team-deleted real-time event to specific member user IDs</summary>
    Task SendTeamDeletedToMembersAsync(Guid teamId, IEnumerable<Guid> memberUserIds, CancellationToken ct = default);

    /// <summary>Sends global team-deleted broadcast</summary>
    Task SendTeamDeletedAsync(Guid teamId, CancellationToken ct = default);

    /// <summary>Maps tournament to DTO and sends real-time tournament update</summary>
    Task SendTournamentUpdatedAsync(Tournament tournament, CancellationToken ct = default);

    /// <summary>Sends player-removed-from-team real-time event</summary>
    Task SendRemovedFromTeamAsync(Guid userId, Guid teamId, Guid playerId, CancellationToken ct = default);

    /// <summary>Sends a push notification using a template</summary>
    Task NotifyByTemplateAsync(Guid userId, string templateKey,
        Dictionary<string, string>? placeholders = null,
        Guid? entityId = null, string? entityType = null,
        CancellationToken ct = default);

    /// <summary>Sends a push notification with explicit metadata</summary>
    Task NotifyAsync(Guid userId, string title, string message,
        Domain.Enums.NotificationCategory category = Domain.Enums.NotificationCategory.System,
        Domain.Enums.NotificationType type = Domain.Enums.NotificationType.Info,
        CancellationToken ct = default);
}
