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
        await base.OnConnectedAsync();
    }
}
