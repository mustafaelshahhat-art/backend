using System;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Api.Hubs;
using Microsoft.AspNetCore.SignalR;

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
}
