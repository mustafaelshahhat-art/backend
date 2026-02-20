using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Matches;
using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

/// <summary>
/// Aggregates real-time, analytics, and push notification concerns for match events.
/// Reduces deps across match handlers.
/// </summary>
public class MatchEventNotifierService : IMatchEventNotifier
{
    private readonly IRealTimeNotifier _notifier;
    private readonly IActivityLogger _activityLogger;
    private readonly INotificationService _notificationService;
    private readonly IMapper _mapper;

    public MatchEventNotifierService(
        IRealTimeNotifier notifier,
        IActivityLogger activityLogger,
        INotificationService notificationService,
        IMapper mapper)
    {
        _notifier = notifier;
        _activityLogger = activityLogger;
        _notificationService = notificationService;
        _mapper = mapper;
    }

    public async Task SendMatchUpdateAsync(Match match, CancellationToken ct = default)
    {
        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto, ct);
    }

    public async Task SendTournamentUpdateAsync(Tournament tournament, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TournamentDto>(tournament);
        await _notifier.SendTournamentUpdatedAsync(dto, ct);
    }

    public Task LogActivityAsync(string templateCode, Dictionary<string, string> placeholders,
        Guid? userId = null, string? userName = null, CancellationToken ct = default)
    {
        return _activityLogger.LogAsync(templateCode, placeholders, userId, userName, ct);
    }

    public Task SendNotificationAsync(Guid userId, string title, string message,
        NotificationCategory category = NotificationCategory.System,
        NotificationType type = NotificationType.Info,
        CancellationToken ct = default)
    {
        return _notificationService.SendNotificationAsync(userId, title, message, category, type, ct: ct);
    }

    public async Task HandleLifecycleOutcomeAsync(TournamentLifecycleResult result, CancellationToken ct = default)
    {
        if (result.GroupsFinished)
        {
            await _activityLogger.LogAsync(
                ActivityConstants.GROUPS_FINISHED,
                new Dictionary<string, string> { { "tournamentName", result.TournamentName ?? "" } },
                null, "System", ct);
        }

        if (result.GroupsFinished && result.ManualQualificationRequired)
        {
            await _notificationService.SendNotificationAsync(Guid.Empty,
                "تأهيل يدوي مطلوب",
                $"انتهت مرحلة المجموعات لبطولة {result.TournamentName}. يرجى تحديد الفرق المتأهلة يدوياً للأدوار الإقصائية.",
                NotificationCategory.Tournament, NotificationType.Info, ct: ct);
        }

        if (result.NextRoundGenerated && result.GroupsFinished && !result.ManualQualificationRequired)
        {
            await _activityLogger.LogAsync(
                ActivityConstants.KNOCKOUT_STARTED,
                new Dictionary<string, string> { { "tournamentName", result.TournamentName ?? "" } },
                null, "System", ct);
            await _notificationService.SendNotificationAsync(Guid.Empty,
                "بدء الأدوار الإقصائية",
                $"تأهلت الفرق وبدأت الأدوار الإقصائية لبطولة {result.TournamentName}",
                NotificationCategory.Tournament, ct: ct);
        }

        if (result.TournamentFinalized)
        {
            await _activityLogger.LogAsync(
                ActivityConstants.TOURNAMENT_FINALIZED,
                new Dictionary<string, string>
                {
                    { "tournamentName", result.TournamentName ?? "" },
                    { "winnerName", result.WinnerTeamName ?? "Unknown" }
                },
                null, "نظام", ct);
            await _notificationService.SendNotificationAsync(Guid.Empty,
                "القمة انتهت!",
                $"انتهت بطولة {result.TournamentName} رسمياً وتوج فريق {result.WinnerTeamName} باللقب!",
                NotificationCategory.Tournament, NotificationType.Success, ct: ct);
        }
    }
}
