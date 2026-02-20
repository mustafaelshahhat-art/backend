using Application.DTOs.Teams;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TeamRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeamRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("my-invitations")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<JoinRequestDto>>> GetMyInvitations([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var query = new Application.Features.TeamRequests.Queries.GetMyInvitations.GetMyInvitationsQuery(userId, page, pageSize);
        var invitations = await _mediator.Send(query, cancellationToken);
        return Ok(invitations);
    }

    [HttpGet("for-my-team")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<JoinRequestDto>>> GetForMyTeam([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var query = new Application.Features.TeamRequests.Queries.GetRequestsForCaptain.GetRequestsForCaptainQuery(userId, page, pageSize);
        var requests = await _mediator.Send(query, cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{id}/accept")]
    public async Task<ActionResult<JoinRequestDto>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var command = new Application.Features.TeamRequests.Commands.AcceptInvite.AcceptInviteCommand(id, userId);
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult<JoinRequestDto>> Reject(Guid id, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var command = new Application.Features.TeamRequests.Commands.RejectInvite.RejectInviteCommand(id, userId);
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }
}
