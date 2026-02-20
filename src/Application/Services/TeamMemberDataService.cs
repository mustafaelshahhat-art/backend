using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;

namespace Application.Services;

/// <summary>
/// Parameter object grouping User, Player, and TeamJoinRequest repositories.
/// These three are always used together in team member management handlers.
/// </summary>
public class TeamMemberDataService : ITeamMemberDataService
{
    public IRepository<User> Users { get; }
    public IRepository<Player> Players { get; }
    public IRepository<TeamJoinRequest> JoinRequests { get; }

    public TeamMemberDataService(
        IRepository<User> users,
        IRepository<Player> players,
        IRepository<TeamJoinRequest> joinRequests)
    {
        Users = users;
        Players = players;
        JoinRequests = joinRequests;
    }
}
