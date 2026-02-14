using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface IRealTimeNotifier
{
    Task SafeSendNotificationAsync(Guid userId, Notification notification, CancellationToken ct = default);
    Task SendAccountStatusChangedAsync(Guid userId, string newStatus, CancellationToken ct = default);
    Task SendRemovedFromTeamAsync(Guid userId, Guid teamId, Guid playerId, CancellationToken ct = default);
    Task SendTeamDeletedAsync(Guid teamId, System.Collections.Generic.IEnumerable<Guid> userIds, CancellationToken ct = default);
    Task SendMatchUpdatedAsync(Application.DTOs.Matches.MatchDto match, CancellationToken ct = default);
    Task SendMatchCreatedAsync(Application.DTOs.Matches.MatchDto match, CancellationToken ct = default);
    Task SendMatchesGeneratedAsync(System.Collections.Generic.IEnumerable<Application.DTOs.Matches.MatchDto> matches, CancellationToken ct = default);
    Task SendMatchDeletedAsync(Guid matchId, CancellationToken ct = default);

    Task SendTournamentUpdatedAsync(Application.DTOs.Tournaments.TournamentDto tournament, CancellationToken ct = default);
    Task SendTournamentCreatedAsync(Application.DTOs.Tournaments.TournamentDto tournament, CancellationToken ct = default);
    Task SendTournamentDeletedAsync(Guid tournamentId, CancellationToken ct = default);

    Task SendUserUpdatedAsync(Application.DTOs.Users.UserDto user, CancellationToken ct = default);
    Task SendUserCreatedAsync(Application.DTOs.Users.UserDto user, CancellationToken ct = default);
    Task SendUserDeletedAsync(Guid userId, CancellationToken ct = default);

    Task SendTeamCreatedAsync(Application.DTOs.Teams.TeamDto team, CancellationToken ct = default);
    Task SendTeamUpdatedAsync(Application.DTOs.Teams.TeamDto team, CancellationToken ct = default);
    Task SendTeamDeletedAsync(Guid teamId, CancellationToken ct = default); // Global event for lists

    // Payment & Registration Real-Time Events
    Task SendRegistrationUpdatedAsync(Application.DTOs.Tournaments.TeamRegistrationDto registration, CancellationToken ct = default);
    Task SendRegistrationApprovedAsync(Application.DTOs.Tournaments.TournamentDto tournament, CancellationToken ct = default);
    Task SendRegistrationRejectedAsync(Application.DTOs.Tournaments.TournamentDto tournament, CancellationToken ct = default);



    Task SendSystemEventAsync(string type, object metadata, string? group = null, CancellationToken ct = default);
}
