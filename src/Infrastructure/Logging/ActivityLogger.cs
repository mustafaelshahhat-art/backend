using Application.Common.Interfaces;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Logging;

/// <summary>
/// IActivityLogger implementation that delegates to the existing
/// IBackgroundActivityLogger (channel-based background worker).
///
/// Fire-and-forget activity logging — errors are swallowed and logged.
/// Used by domain event handlers to log analytics after side-effects.
///
/// See: EXECUTION_PLAN §2.1, EXECUTION_BLUEPRINT §3.1 row 2
/// </summary>
public class ActivityLogger : IActivityLogger
{
    private readonly IBackgroundActivityLogger _backgroundLogger;
    private readonly ILogger<ActivityLogger> _logger;

    public ActivityLogger(
        IBackgroundActivityLogger backgroundLogger,
        ILogger<ActivityLogger> logger)
    {
        _backgroundLogger = backgroundLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task LogAsync(
        string activityCode,
        Dictionary<string, string> parameters,
        Guid? userId = null,
        string? userName = null,
        CancellationToken ct = default)
    {
        try
        {
            // Delegate to background channel-based logger (non-blocking)
            _backgroundLogger.LogActivityByTemplate(
                activityCode, parameters, userId, userName);
        }
        catch (Exception ex)
        {
            // Activity logging must never break the pipeline
            _logger.LogError(ex,
                "Failed to log activity {ActivityCode} for user {UserId}",
                activityCode, userId);
        }

        return Task.CompletedTask;
    }
}
