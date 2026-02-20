using Domain.Enums;

namespace Application.Common.Interfaces;

/// <summary>
/// Unified notification dispatcher that handles:
/// - Persistent notifications (DB)
/// - Real-time notifications (SignalR)
/// - Template-based notifications
/// 
/// This replaces direct injection of INotificationService + IRealTimeNotifier
/// in command/query handlers. Side-effects should be triggered via
/// domain event handlers that use this dispatcher.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>Send a persistent + real-time notification to a specific user.</summary>
    Task NotifyUserAsync(
        Guid userId,
        string title,
        string message,
        NotificationCategory category = NotificationCategory.System,
        Guid? entityId = null,
        string? entityType = null,
        CancellationToken ct = default);

    /// <summary>Send a template-based notification to a specific user.</summary>
    Task NotifyUserByTemplateAsync(
        Guid userId,
        string templateKey,
        Dictionary<string, string>? parameters = null,
        Guid? entityId = null,
        string? entityType = null,
        CancellationToken ct = default);

    /// <summary>Send notification to all users with a specific role.</summary>
    Task NotifyRoleAsync(
        UserRole role,
        string title,
        string message,
        NotificationCategory category = NotificationCategory.System,
        CancellationToken ct = default);
}
