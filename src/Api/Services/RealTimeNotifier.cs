using System;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Linq;

namespace Api.Services;

public class RealTimeNotifier : IRealTimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public RealTimeNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
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
            Console.WriteLine($"Error sending real-time notification: {ex.Message}");
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
            Console.WriteLine($"Error sending account status update: {ex.Message}");
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
            Console.WriteLine($"Error sending removed from team notification: {ex.Message}");
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
            Console.WriteLine($"Error sending team deleted notification: {ex.Message}");
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
            Console.WriteLine($"Error sending match update: {ex.Message}");
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
             Console.WriteLine($"Error sending match created: {ex.Message}");
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
            Console.WriteLine($"Error sending matches generated: {ex.Message}");
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
            Console.WriteLine($"Error sending match deleted: {ex.Message}");
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
            Console.WriteLine($"Error sending tournament update: {ex.Message}");
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
            Console.WriteLine($"Error sending tournament created: {ex.Message}");
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
            Console.WriteLine($"Error sending tournament deleted: {ex.Message}");
        }
    }

    public async Task SendUserUpdatedAsync(Application.DTOs.Users.UserDto user)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("UserUpdated", user);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending user update: {ex.Message}");
        }
    }

    public async Task SendUserCreatedAsync(Application.DTOs.Users.UserDto user)
    {
        try
        {
            // Only admins usually see this list, but we can broadcast to All or specific Admin group
            await _hubContext.Clients.All.SendAsync("UserCreated", user);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending user created: {ex.Message}");
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
            Console.WriteLine($"Error sending user deleted: {ex.Message}");
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
            Console.WriteLine($"Error sending team created: {ex.Message}");
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
            Console.WriteLine($"Error sending team updated: {ex.Message}");
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
            Console.WriteLine($"Error sending team deleted (global): {ex.Message}");
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
            Console.WriteLine($"Error sending registration updated: {ex.Message}");
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
            Console.WriteLine($"Error sending registration approved: {ex.Message}");
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
            Console.WriteLine($"Error sending registration rejected: {ex.Message}");
        }
    }

    public async Task SendSystemEventAsync(string type, object metadata, string group = null)
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
            Console.WriteLine($"Error sending system event: {ex.Message}");
        }
    }
}
