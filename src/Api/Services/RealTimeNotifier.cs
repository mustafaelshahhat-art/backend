using System;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Notifications;
using Application.Interfaces;
using Domain.Entities;
using Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Api.Services;

public class RealTimeNotifier : IRealTimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<RealTimeNotifier> _logger;

    public RealTimeNotifier(IHubContext<NotificationHub> hubContext, ILogger<RealTimeNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SafeSendNotificationAsync(Guid userId, NotificationDto notification, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending real-time notification");
        }
    }

    public async Task SendAccountStatusChangedAsync(Guid userId, string newStatus, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("AccountStatusChanged", new { UserId = userId, Status = newStatus }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending account status update");
        }
    }

    public async Task SendRemovedFromTeamAsync(Guid userId, Guid teamId, Guid playerId, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("RemovedFromTeam", new { PlayerId = playerId, TeamId = teamId }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending removed from team notification");
        }
    }

    public async Task SendTeamDeletedAsync(Guid teamId, System.Collections.Generic.IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        try
        {
            var userStringIds = System.Linq.Enumerable.Select(userIds, id => id.ToString());
            await _hubContext.Clients.Users(userStringIds.ToList()).SendAsync("TeamDeleted", new { TeamId = teamId }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending team deleted notification");
        }
    }

    public async Task SendMatchUpdatedAsync(Application.DTOs.Matches.MatchDto match, CancellationToken ct = default)
    {
        try
        {
            // PERF: Send match without full Events list over SignalR — clients refetch if needed
            var slim = new {
                match.Id,
                match.TournamentId,
                match.HomeTeamId,
                match.HomeTeamName,
                match.AwayTeamId,
                match.AwayTeamName,
                match.HomeScore,
                match.AwayScore,
                match.GroupId,
                match.RoundNumber,
                match.StageName,
                match.Status,
                match.Date,
                EventCount = match.Events?.Count ?? 0
            };
            await _hubContext.Clients.All.SendAsync("MatchUpdated", slim, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending match update");
        }
    }

    public async Task SendMatchCreatedAsync(Application.DTOs.Matches.MatchDto match, CancellationToken ct = default)
    {
        try
        {
            var slim = new {
                match.Id,
                match.TournamentId,
                match.HomeTeamId,
                match.HomeTeamName,
                match.AwayTeamId,
                match.AwayTeamName,
                match.HomeScore,
                match.AwayScore,
                match.GroupId,
                match.RoundNumber,
                match.StageName,
                match.Status,
                match.Date
            };
            await _hubContext.Clients.All.SendAsync("MatchCreated", slim, ct);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error sending match created");
        }
    }

    public async Task SendMatchesGeneratedAsync(System.Collections.Generic.IEnumerable<Application.DTOs.Matches.MatchDto> matches, CancellationToken ct = default)
    {
        try
        {
            var firstMatch = matches.FirstOrDefault();
            if (firstMatch != null)
            {
                // PERF: Send slim match list without Events
                var slimMatches = matches.Select(m => new {
                    m.Id,
                    m.TournamentId,
                    m.HomeTeamId,
                    m.HomeTeamName,
                    m.AwayTeamId,
                    m.AwayTeamName,
                    m.HomeScore,
                    m.AwayScore,
                    m.GroupId,
                    m.RoundNumber,
                    m.StageName,
                    m.Status,
                    m.Date
                });
                await _hubContext.Clients.All.SendAsync("MatchesGenerated", slimMatches, ct);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("MatchesGenerated", Array.Empty<object>(), ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending matches generated");
        }
    }

    public async Task SendMatchDeletedAsync(Guid matchId, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("MatchDeleted", matchId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending match deleted");
        }
    }

    public async Task SendTournamentUpdatedAsync(Application.DTOs.Tournaments.TournamentDto tournament, CancellationToken ct = default)
    {
        try
        {
            // PERF: Send slim tournament — strip Registrations, Rules, Prizes, Description, payment fields
            var slim = new {
                tournament.Id,
                tournament.Name,
                tournament.Status,
                tournament.Mode,
                tournament.StartDate,
                tournament.EndDate,
                tournament.MaxTeams,
                tournament.CurrentTeams,
                tournament.Format,
                tournament.WinnerTeamId,
                tournament.WinnerTeamName,
                tournament.ImageUrl,
                tournament.RequiresAdminIntervention,
                tournament.CreatedAt,
                tournament.UpdatedAt
            };
            await _hubContext.Clients.All.SendAsync("TournamentUpdated", slim, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending tournament update");
        }
    }

    public async Task SendTournamentCreatedAsync(Application.DTOs.Tournaments.TournamentDto tournament, CancellationToken ct = default)
    {
        try
        {
            // PERF: Slim tournament for Clients.All broadcast
            var slim = new {
                tournament.Id,
                tournament.Name,
                tournament.Status,
                tournament.Mode,
                tournament.StartDate,
                tournament.EndDate,
                tournament.MaxTeams,
                tournament.CurrentTeams,
                tournament.Format,
                tournament.ImageUrl,
                tournament.Location,
                tournament.EntryFee,
                tournament.CreatedAt
            };
            await _hubContext.Clients.All.SendAsync("TournamentCreated", slim, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending tournament created");
        }
    }

    public async Task SendTournamentDeletedAsync(Guid tournamentId, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TournamentDeleted", tournamentId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending tournament deleted");
        }
    }

    public async Task SendUserUpdatedAsync(Application.DTOs.Users.UserDto user, CancellationToken ct = default)
    {
        try
        {
            // Broadcast restricted view to All to protect sensitive data (NationalID, Phone)
            var publicView = new {
                user.Id,
                user.DisplayId,
                user.Name,
                user.Role,
                user.GovernorateId,
                user.GovernorateNameAr,
                user.CityId,
                user.CityNameAr,
                user.TeamId,
                user.TeamName,
                user.TeamRole,
                user.Status
            };
            await _hubContext.Clients.All.SendAsync("UserUpdated", publicView, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user update");
        }
    }

    public async Task SendUserCreatedAsync(Application.DTOs.Users.UserDto user, CancellationToken ct = default)
    {
        try
        {
            var publicView = new {
                user.Id,
                user.DisplayId,
                user.Name,
                user.Role,
                user.GovernorateId,
                user.GovernorateNameAr,
                user.CityId,
                user.CityNameAr,
                user.TeamId,
                user.TeamName,
                user.TeamRole,
                user.Status
            };
            await _hubContext.Clients.All.SendAsync("UserCreated", publicView, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user created");
        }
    }

    public async Task SendUserDeletedAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("UserDeleted", userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user deleted");
        }
    }

    public async Task SendTeamCreatedAsync(Application.DTOs.Teams.TeamDto team, CancellationToken ct = default)
    {
        try
        {
            // PERF: Strip Players list from Clients.All broadcast
            var slim = new {
                team.Id,
                team.Name,
                team.CaptainName,
                team.Founded,
                team.City,
                team.IsActive,
                team.PlayerCount,
                team.MaxPlayers
            };
            await _hubContext.Clients.All.SendAsync("TeamCreated", slim, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending team created");
        }
    }

    public async Task SendTeamUpdatedAsync(Application.DTOs.Teams.TeamDto team, CancellationToken ct = default)
    {
        try
        {
            // PERF: Strip Players list from Clients.All broadcast
            var slim = new {
                team.Id,
                team.Name,
                team.CaptainName,
                team.Founded,
                team.City,
                team.IsActive,
                team.PlayerCount,
                team.MaxPlayers,
                team.Stats
            };
            await _hubContext.Clients.All.SendAsync("TeamUpdated", slim, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending team updated");
        }
    }
    
    // New global delete method overload
    public async Task SendTeamDeletedAsync(Guid teamId, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TeamDeleted", teamId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending team deleted (global)");
        }
    }

    public async Task SendRegistrationUpdatedAsync(Application.DTOs.Tournaments.TeamRegistrationDto registration, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("RegistrationUpdated", registration, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending registration updated");
        }
    }

    public async Task SendRegistrationApprovedAsync(Application.DTOs.Tournaments.TournamentDto tournament, CancellationToken ct = default)
    {
        try
        {
            // PERF: Only send essential fields for registration status change
            var slim = new {
                tournament.Id,
                tournament.Name,
                tournament.Status,
                tournament.CurrentTeams,
                tournament.MaxTeams
            };
            await _hubContext.Clients.All.SendAsync("RegistrationApproved", slim, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending registration approved");
        }
    }

    public async Task SendRegistrationRejectedAsync(Application.DTOs.Tournaments.TournamentDto tournament, CancellationToken ct = default)
    {
        try
        {
            var slim = new {
                tournament.Id,
                tournament.Name,
                tournament.Status,
                tournament.CurrentTeams,
                tournament.MaxTeams
            };
            await _hubContext.Clients.All.SendAsync("RegistrationRejected", slim, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending registration rejected");
        }
    }



    public async Task SendToRoleGroupAsync(string role, NotificationDto notification, CancellationToken ct = default)
    {
        try
        {
            await _hubContext.Clients.Group($"role:{role}")
                .SendAsync("ReceiveNotification", notification, ct);
            _logger.LogDebug("Sent notification to role group {Role}", role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to role group {Role}", role);
        }
    }

    public async Task SendSystemEventAsync(string type, object metadata, string? group = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new { Type = type, Metadata = metadata, Timestamp = DateTime.UtcNow };
            if (!string.IsNullOrEmpty(group))
            {
                await _hubContext.Clients.Group(group).SendAsync("SystemEvent", payload, ct);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("SystemEvent", payload, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending system event");
        }
    }
}
