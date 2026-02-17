using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Hubs;

[Authorize]
public class MatchChatHub : Hub
{
    private readonly IMatchMessageRepository _messageRepository;
    private readonly Domain.Interfaces.IRepository<Match> _matchRepository;
    private readonly Application.Interfaces.IUserService _userService;
    private readonly Domain.Interfaces.IRepository<Player> _playerRepository;
    private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
    private readonly ICurrentUserAccessor _userAccessor;

    public MatchChatHub(
        IMatchMessageRepository messageRepository,
        Domain.Interfaces.IRepository<Match> matchRepository,
        Domain.Interfaces.IRepository<Player> playerRepository,
        Application.Interfaces.IUserService userService,
        ICurrentUserAccessor userAccessor,
        Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
    {
        _messageRepository = messageRepository;
        _matchRepository = matchRepository;
        _playerRepository = playerRepository;
        _userService = userService;
        _userAccessor = userAccessor;
        _cache = cache;
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
        if (role != "Admin")
        {
            // PERFORMANCE FIX: Cache match participant checks for 5 minutes.
            // This prevents a DB query on every single chat message.
            var cacheKey = $"match_access_{matchId}_{userId}";
            
            if (!_cache.TryGetValue(cacheKey, out bool hasAccess))
            {
                hasAccess = await VerifyMatchAccessAsync(matchId, userId);
                var cacheOptions = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                cacheOptions.Size = 64; // Boolean + cache key overhead
                
                _cache.Set(cacheKey, hasAccess, cacheOptions);
            }

            if (!hasAccess) return;
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

    private async Task<bool> VerifyMatchAccessAsync(string matchId, Guid userId)
    {
        if (!Guid.TryParse(matchId, out var matchGuid)) return false;

        // Use lean projection to check both participants AND tournament creator
        var matchAccessInfo = await _matchRepository.GetQueryable()
            .AsNoTracking()
            .Where(m => m.Id == matchGuid)
            .Select(m => new 
            { 
                m.HomeTeamId, 
                m.AwayTeamId, 
                TournamentCreatorId = m.Tournament!.CreatorUserId 
            })
            .FirstOrDefaultAsync();

        if (matchAccessInfo == null) return false;

        // 1. Check if user is the Tournament Creator
        if (matchAccessInfo.TournamentCreatorId == userId) return true;

        // 2. Check if user belongs to one of the teams participating in the match
        // We check the Player table for any membership of this user in either team
        var hasMembership = await _playerRepository.GetQueryable()
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId && (p.TeamId == matchAccessInfo.HomeTeamId || p.TeamId == matchAccessInfo.AwayTeamId));

        return hasMembership;
    }
}
