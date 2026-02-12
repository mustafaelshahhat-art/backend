using Microsoft.AspNetCore.Authorization;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Infrastructure.Authorization;

public class TeamCaptainRequirement : IAuthorizationRequirement { }

public class TeamCaptainHandler : AuthorizationHandler<TeamCaptainRequirement>
{
    private readonly IRepository<Player> _playerRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TeamCaptainHandler(IRepository<Player> playerRepository, IHttpContextAccessor httpContextAccessor)
    {
        _playerRepository = playerRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TeamCaptainRequirement requirement)
    {
        var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return;
        }

        // Admins pass anything
        if (context.User.IsInRole(UserRole.Admin.ToString()))
        {
            context.Succeed(requirement);
            return;
        }

        // Check if user is a Captain in the specific team they are trying to manage
        // The teamId is usually in the route
        var routeValues = _httpContextAccessor.HttpContext?.Request.RouteValues;
        if (routeValues != null && (routeValues.TryGetValue("teamId", out var teamIdObj) || routeValues.TryGetValue("id", out teamIdObj)))
        {
            if (Guid.TryParse(teamIdObj?.ToString(), out var teamId))
            {
                var players = await _playerRepository.FindAsync(p => p.TeamId == teamId && p.UserId == userId);
                var player = players.FirstOrDefault();
                
                if (player != null && player.TeamRole == TeamRole.Captain)
                {
                    context.Succeed(requirement);
                }
            }
        }
    }
}
