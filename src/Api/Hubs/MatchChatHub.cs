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
    private readonly Domain.Interfaces.IRepository<Match> _matchRepository;
    private readonly Application.Interfaces.IUserService _userService;

    public MatchChatHub(
        IMatchMessageRepository messageRepository,
        Domain.Interfaces.IRepository<Match> matchRepository,
        Application.Interfaces.IUserService userService)
    {
        _messageRepository = messageRepository;
        _matchRepository = matchRepository;
        _userService = userService;
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
        var userIdStr = Context.UserIdentifier;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId)) return;

        var userName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value ?? "Player";

        // Security Check: Verify user belongs to the match
        if (role != "Admin" && role != "Referee")
        {
            var user = await _userService.GetByIdAsync(userId);
            if (user == null || !user.TeamId.HasValue) return;

            var matchGuid = Guid.Parse(matchId);
            var match = await _matchRepository.GetByIdAsync(matchGuid);
            if (match == null) return;

            if (match.HomeTeamId != user.TeamId && match.AwayTeamId != user.TeamId)
            {
                // User is in a team but NOT one of the teams in this match. Rejected.
                return;
            }
        }

        var message = new MatchMessage
        {
            MatchId = Guid.Parse(matchId),
            SenderId = userId,
            SenderName = userName,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        // Persist
        await _messageRepository.AddAsync(message);

        // Broadcast to group
        await Clients.Group($"match-{matchId}").SendAsync("ReceiveMessage", message);
    }
}
