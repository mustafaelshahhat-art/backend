using Application.DTOs.Teams;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TeamRequestsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamRequestsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet("my-invitations")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<JoinRequestDto>>> GetMyInvitations([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var invitations = await _teamService.GetUserInvitationsAsync(userId, page, pageSize, cancellationToken);
        return Ok(invitations);
    }

    [HttpGet("for-my-team")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<JoinRequestDto>>> GetForMyTeam([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        // Find teams where user is captain
        var requests = await _teamService.GetRequestsForCaptainAsync(userId, page, pageSize, cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{id}/accept")]
    public async Task<ActionResult<JoinRequestDto>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var response = await _teamService.AcceptInviteAsync(id, userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult<JoinRequestDto>> Reject(Guid id, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var response = await _teamService.RejectInviteAsync(id, userId, cancellationToken);
        return Ok(response);
    }
}
