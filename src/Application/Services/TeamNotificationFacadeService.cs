using Application.DTOs.Teams;
using Application.DTOs.Tournaments;
using Application.DTOs.Users;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using System.Linq.Expressions;

namespace Application.Services;

/// <summary>
/// Aggregates real-time, push notification, and mapping concerns for team member events.
/// Reduces 3 deps (IRealTimeNotifier, INotificationService, IMapper) to 1 injectable service.
/// Also handles team snapshot loading for real-time broadcasts.
/// </summary>
public class TeamNotificationFacadeService : ITeamNotificationFacade
{
    private readonly IRealTimeNotifier _notifier;
    private readonly INotificationService _notificationService;
    private readonly IMapper _mapper;
    private readonly IRepository<Team> _teamRepository;

    public TeamNotificationFacadeService(
        IRealTimeNotifier notifier,
        INotificationService notificationService,
        IMapper mapper,
        IRepository<Team> teamRepository)
    {
        _notifier = notifier;
        _notificationService = notificationService;
        _mapper = mapper;
        _teamRepository = teamRepository;
    }

    public async Task SendUserUpdatedAsync(User user, CancellationToken ct = default)
    {
        var dto = _mapper.Map<UserDto>(user);
        await _notifier.SendUserUpdatedAsync(dto, ct);
    }

    public Task SendUserCreatedAsync(UserDto userDto, CancellationToken ct = default)
    {
        return _notifier.SendUserCreatedAsync(userDto, ct);
    }

    public async Task SendTeamUpdatedAsync(Guid teamId, CancellationToken ct = default)
    {
        var teamSnapshot = await _teamRepository.GetByIdNoTrackingAsync(teamId,
            new Expression<Func<Team, object>>[] { t => t.Players, t => t.Statistics! }, ct);
        if (teamSnapshot != null)
        {
            var dto = _mapper.Map<TeamDto>(teamSnapshot);
            await _notifier.SendTeamUpdatedAsync(dto, ct);
        }
    }

    public Task SendTeamDeletedToMembersAsync(Guid teamId, IEnumerable<Guid> memberUserIds, CancellationToken ct = default)
    {
        return _notifier.SendTeamDeletedAsync(teamId, memberUserIds, ct);
    }

    public Task SendTeamDeletedAsync(Guid teamId, CancellationToken ct = default)
    {
        return _notifier.SendTeamDeletedAsync(teamId, ct);
    }

    public async Task SendTournamentUpdatedAsync(Tournament tournament, CancellationToken ct = default)
    {
        var dto = _mapper.Map<TournamentDto>(tournament);
        await _notifier.SendTournamentUpdatedAsync(dto, ct);
    }

    public Task SendRemovedFromTeamAsync(Guid userId, Guid teamId, Guid playerId, CancellationToken ct = default)
    {
        return _notifier.SendRemovedFromTeamAsync(userId, teamId, playerId, ct);
    }

    public Task NotifyByTemplateAsync(Guid userId, string templateKey,
        Dictionary<string, string>? placeholders = null,
        Guid? entityId = null, string? entityType = null,
        CancellationToken ct = default)
    {
        return _notificationService.SendNotificationByTemplateAsync(userId, templateKey, placeholders, entityId, entityType, ct: ct);
    }

    public Task NotifyAsync(Guid userId, string title, string message,
        NotificationCategory category = NotificationCategory.System,
        NotificationType type = NotificationType.Info,
        CancellationToken ct = default)
    {
        return _notificationService.SendNotificationAsync(userId, title, message, category, type, ct: ct);
    }
}
