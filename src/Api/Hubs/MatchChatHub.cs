using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public class MatchChatHub : Hub
{
    private readonly IMatchMessageRepository _messageRepository;

    public MatchChatHub(IMatchMessageRepository messageRepository)
    {
        _messageRepository = messageRepository;
    }

    public async Task JoinMatchGroup(string matchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"match-{matchId}");
    }

    public async Task LeaveMatchGroup(string matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match-{matchId}");
    }

    public async Task SendMessage(string matchId, string content)
    {
        var userId = Context.UserIdentifier;
        var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Player";

        if (string.IsNullOrEmpty(userId)) return;

        var message = new MatchMessage
        {
            MatchId = Guid.Parse(matchId),
            SenderId = Guid.Parse(userId),
            SenderName = userName,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        // Persist
        await _messageRepository.AddAsync(message);

        // Broadcast to group
        // Frontend expects: event "ReceiveMessage", payload messageDto
        await Clients.Group($"match-{matchId}").SendAsync("ReceiveMessage", message);
    }
}
