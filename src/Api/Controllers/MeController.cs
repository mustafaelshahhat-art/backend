using Application.DTOs.Teams;
using Application.DTOs.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IMediator _mediator;

    public MeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<UserDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var query = new Application.Features.Users.Queries.GetUserById.GetUserByIdQuery(Guid.Parse(userId));
        var user = await _mediator.Send(query, cancellationToken);
        if (user == null) return NotFound();
        return Ok(user);
    }

    [HttpGet("teams-overview")]
    public async Task<ActionResult<TeamsOverviewDto>> GetTeamsOverview(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var query = new Application.Features.Teams.Queries.GetTeamsOverview.GetTeamsOverviewQuery(Guid.Parse(userId));
        var overview = await _mediator.Send(query, cancellationToken);
        return Ok(overview);
    }
}