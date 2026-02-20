namespace Application.Common.Interfaces;

/// <summary>
/// Abstraction for activity/analytics logging.
/// Replaces direct IAnalyticsService injection in command handlers.
/// Side-effects (analytics) should be triggered via domain event handlers.
/// </summary>
public interface IActivityLogger
{
    Task LogAsync(
        string activityCode,
        Dictionary<string, string> parameters,
        Guid? userId = null,
        string? userName = null,
        CancellationToken ct = default);
}
