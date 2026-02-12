using System;
using System.Threading.Tasks;
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

    public async Task SafeSendNotificationAsync(Guid userId, Notification notification)
    {
        try
        {
             // Assumes UserId is mapped to SignalR User Identifier
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", notification);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the operation
            _logger.LogError(ex, "Error sending real-time notification");
        }
    }

    public async Task SendAccountStatusChangedAsync(Guid userId, string newStatus)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("AccountStatusChanged", new { UserId = userId, Status = newStatus });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending account status update");
        }
    }

    public async Task SendRemovedFromTeamAsync(Guid userId, Guid teamId, Guid playerId)
    {
        try
        {
            await _hubContext.Clients.User(userId.ToString()).SendAsync("RemovedFromTeam", new { PlayerId = playerId, TeamId = teamId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending removed from team notification");
        }
    }

    public async Task SendTeamDeletedAsync(Guid teamId, System.Collections.Generic.IEnumerable<Guid> userIds)
    {
        try
        {
            var userStringIds = System.Linq.Enumerable.Select(userIds, id => id.ToString());
            await _hubContext.Clients.Users(userStringIds.ToList()).SendAsync("TeamDeleted", new { TeamId = teamId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending team deleted notification");
        }
    }

    public async Task SendMatchUpdatedAsync(Application.DTOs.Matches.MatchDto match)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("MatchUpdated", match);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending match update");
        }
    }

    public async Task SendMatchCreatedAsync(Application.DTOs.Matches.MatchDto match)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("MatchCreated", match);
        }
        catch (Exception ex)
        {
             _logger.LogError(ex, "Error sending match created");
        }
    }

    public async Task SendMatchesGeneratedAsync(System.Collections.Generic.IEnumerable<Application.DTOs.Matches.MatchDto> matches)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("MatchesGenerated", matches);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending matches generated");
        }
    }

    public async Task SendMatchDeletedAsync(Guid matchId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("MatchDeleted", matchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending match deleted");
        }
    }

    public async Task SendTournamentUpdatedAsync(Application.DTOs.Tournaments.TournamentDto tournament)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TournamentUpdated", tournament);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending tournament update");
        }
    }

    public async Task SendTournamentCreatedAsync(Application.DTOs.Tournaments.TournamentDto tournament)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TournamentCreated", tournament);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending tournament created");
        }
    }

    public async Task SendTournamentDeletedAsync(Guid tournamentId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TournamentDeleted", tournamentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending tournament deleted");
        }
    }

    public async Task SendUserUpdatedAsync(Application.DTOs.Users.UserDto user)
    {
        try
        {
            // Broadcast restricted view to All to protect sensitive data (NationalID, Phone)
            var publicView = new {
                user.Id,
                user.DisplayId,
                user.Name,
                user.Role,
                user.Avatar,
                user.Governorate,
                user.City,
                user.TeamId,
                user.TeamName,
                user.TeamRole,
                user.Status
            };
            await _hubContext.Clients.All.SendAsync("UserUpdated", publicView);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user update");
        }
    }

    public async Task SendUserCreatedAsync(Application.DTOs.Users.UserDto user)
    {
        try
        {
            var publicView = new {
                user.Id,
                user.DisplayId,
                user.Name,
                user.Role,
                user.Avatar,
                user.Governorate,
                user.City,
                user.TeamId,
                user.TeamName,
                user.TeamRole,
                user.Status
            };
            await _hubContext.Clients.All.SendAsync("UserCreated", publicView);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user created");
        }
    }

    public async Task SendUserDeletedAsync(Guid userId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("UserDeleted", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user deleted");
        }
    }

    public async Task SendTeamCreatedAsync(Application.DTOs.Teams.TeamDto team)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TeamCreated", team);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending team created");
        }
    }

    public async Task SendTeamUpdatedAsync(Application.DTOs.Teams.TeamDto team)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TeamUpdated", team);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending team updated");
        }
    }
    
    // New global delete method overload
    public async Task SendTeamDeletedAsync(Guid teamId)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("TeamDeleted", teamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending team deleted (global)");
        }
    }

    public async Task SendRegistrationUpdatedAsync(Application.DTOs.Tournaments.TeamRegistrationDto registration)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("RegistrationUpdated", registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending registration updated");
        }
    }

    public async Task SendRegistrationApprovedAsync(Application.DTOs.Tournaments.TournamentDto tournament)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("RegistrationApproved", tournament);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending registration approved");
        }
    }

    public async Task SendRegistrationRejectedAsync(Application.DTOs.Tournaments.TournamentDto tournament)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("RegistrationRejected", tournament);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending registration rejected");
        }
    }

    public async Task SendObjectionSubmittedAsync(Application.DTOs.Objections.ObjectionDto objection)
    {
        try
        {
            // Security: Only admins and the submitting team should know about the objection
            await _hubContext.Clients.Group("role:Admin").SendAsync("ObjectionSubmitted", objection);
            
            // Notify the specific captain
            if (objection.CaptainId != Guid.Empty)
            {
                await _hubContext.Clients.User(objection.CaptainId.ToString()).SendAsync("ObjectionSubmitted", objection);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending objection submitted");
        }
    }

    public async Task SendObjectionResolvedAsync(Application.DTOs.Objections.ObjectionDto objection)
    {
        try
        {
            // Security: Broadcast resolution only to Admins and the involved captain
            await _hubContext.Clients.Group("role:Admin").SendAsync("ObjectionResolved", objection);
            
            if (objection.CaptainId != Guid.Empty)
            {
                await _hubContext.Clients.User(objection.CaptainId.ToString()).SendAsync("ObjectionResolved", objection);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending objection resolved");
        }
    }

    public async Task SendSystemEventAsync(string type, object metadata, string? group = null)
    {
        try
        {
            var payload = new { Type = type, Metadata = metadata, Timestamp = DateTime.UtcNow };
            if (!string.IsNullOrEmpty(group))
            {
                await _hubContext.Clients.Group(group).SendAsync("SystemEvent", payload);
            }
            else
            {
                await _hubContext.Clients.All.SendAsync("SystemEvent", payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending system event");
        }
    }
}
