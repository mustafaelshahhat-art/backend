using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    // Users automatically join a group based on their UserId by default UserIdentifier?
    // Or we manually map them.
    // Best practice: Use IUserIdProvider or just target Clients.User(userId).
    // If ClaimsPrincipal has NameIdentifier (sub), SignalR uses it by default.
    // We can also have groups.

    public override async Task OnConnectedAsync()
    {
        // Automatically join user-specific group
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        await base.OnConnectedAsync();
    }

    public async Task SubscribeToRole(string role)
    {
        // SECURITY FIX: Verify the user actually has the role they are trying to subscribe to.
        // This prevents a normal user from subscribing to "Admin" notifications.
        if (Context.User == null || !Context.User.IsInRole(role))
        {
            throw new HubException($"Unauthorized: You do not possess the role '{role}'.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");
    }

    public async Task UnsubscribeFromRole(string role)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"role:{role}");
    }

    public async Task SubscribeToTournament(string tournamentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"tournament:{tournamentId}");
    }

    public async Task UnsubscribeFromTournament(string tournamentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tournament:{tournamentId}");
    }

    public async Task SubscribeToMatch(string matchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"match:{matchId}");
    }

    public async Task UnsubscribeFromMatch(string matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match:{matchId}");
    }
}
