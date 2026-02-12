using Microsoft.AspNetCore.Authorization;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Infrastructure.Authorization;

public class TournamentOwnerRequirement : IAuthorizationRequirement { }

public class TournamentOwnerHandler : AuthorizationHandler<TournamentOwnerRequirement>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TournamentOwnerHandler(IRepository<Tournament> tournamentRepository, IHttpContextAccessor httpContextAccessor)
    {
        _tournamentRepository = tournamentRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TournamentOwnerRequirement requirement)
    {
        var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId)) return;

        // Admins pass anything
        if (context.User.IsInRole(UserRole.Admin.ToString()))
        {
            context.Succeed(requirement);
            return;
        }

        var routeValues = _httpContextAccessor.HttpContext?.Request.RouteValues;
        if (routeValues != null && routeValues.TryGetValue("id", out var idObj))
        {
            if (Guid.TryParse(idObj?.ToString(), out var tournamentId))
            {
                var tournament = await _tournamentRepository.GetByIdAsync(tournamentId);
                if (tournament != null && tournament.CreatorUserId == userId)
                {
                    context.Succeed(requirement);
                }
            }
        }
    }
}
