using Domain.Entities;
using Domain.Interfaces;

namespace Application.Interfaces;

/// <summary>
/// Parameter object that groups the three team-membership-related repositories
/// always used together in team member management handlers.
/// </summary>
public interface ITeamMemberDataService
{
    IRepository<User> Users { get; }
    IRepository<Player> Players { get; }
    IRepository<TeamJoinRequest> JoinRequests { get; }
}
