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
}
