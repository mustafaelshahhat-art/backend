using System;
using System.Threading.Tasks;
using Domain.Entities;

namespace Application.Interfaces;

public interface IRealTimeNotifier
{
    Task SafeSendNotificationAsync(Guid userId, Notification notification);
    Task SendAccountStatusChangedAsync(Guid userId, string newStatus);
    Task SendRemovedFromTeamAsync(Guid userId, Guid teamId, Guid playerId);
    Task SendTeamDeletedAsync(Guid teamId, System.Collections.Generic.IEnumerable<Guid> userIds);
    Task SendMatchUpdatedAsync(Application.DTOs.Matches.MatchDto match);
    Task SendMatchCreatedAsync(Application.DTOs.Matches.MatchDto match);
    Task SendMatchesGeneratedAsync(System.Collections.Generic.IEnumerable<Application.DTOs.Matches.MatchDto> matches);
    Task SendMatchDeletedAsync(Guid matchId);

    Task SendTournamentUpdatedAsync(Application.DTOs.Tournaments.TournamentDto tournament);
    Task SendTournamentCreatedAsync(Application.DTOs.Tournaments.TournamentDto tournament);
    Task SendTournamentDeletedAsync(Guid tournamentId);

    Task SendUserUpdatedAsync(Application.DTOs.Users.UserDto user);
    Task SendUserCreatedAsync(Application.DTOs.Users.UserDto user);
    Task SendUserDeletedAsync(Guid userId);

    Task SendTeamCreatedAsync(Application.DTOs.Teams.TeamDto team);
    Task SendTeamUpdatedAsync(Application.DTOs.Teams.TeamDto team);
    Task SendTeamDeletedAsync(Guid teamId); // Global event for lists

    // Payment & Registration Real-Time Events
    Task SendRegistrationUpdatedAsync(Application.DTOs.Tournaments.TeamRegistrationDto registration);
    Task SendRegistrationApprovedAsync(Application.DTOs.Tournaments.TournamentDto tournament);
    Task SendRegistrationRejectedAsync(Application.DTOs.Tournaments.TournamentDto tournament);



    Task SendSystemEventAsync(string type, object metadata, string? group = null);
}
