using Application.Contracts.Common;
using Application.Contracts.Teams.Responses;
using Application.DTOs.Teams;
using Application.DTOs.Tournaments;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeamsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [OutputCache(PolicyName = "TeamList")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TeamDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] Guid? captainId = null, [FromQuery] Guid? playerId = null, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Teams.Queries.GetTeamsPaged.GetTeamsPagedQuery(page, pageSize, captainId, playerId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [OutputCache(PolicyName = "TeamDetail")]
    public async Task<ActionResult<TeamDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new Application.Features.Teams.Queries.GetTeamById.GetTeamByIdQuery(id);
        var team = await _mediator.Send(query, cancellationToken);
        if (team == null) return NotFound();
        return Ok(team);
    }

    [HttpPost]
    public async Task<ActionResult<TeamDto>> Create(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var command = new Application.Features.Teams.Commands.CreateTeam.CreateTeamCommand(request, Guid.Parse(userId));
        var team = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = team.Id }, team);
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<TeamDto>> Update(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Teams.Commands.UpdateTeam.UpdateTeamCommand(id, request, userId, userRole);
        var updatedTeam = await _mediator.Send(command, cancellationToken);
        return Ok(updatedTeam);
    }

    [HttpPost("{id}/join")]
    public async Task<ActionResult<JoinRequestDto>> RequestJoin(Guid id, CancellationToken cancellationToken) 
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var command = new Application.Features.Teams.Commands.RequestJoinTeam.RequestJoinTeamCommand(id, Guid.Parse(userId));
        var result = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{id}/join-requests")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<JoinRequestDto>>> GetJoinRequests(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Teams.Queries.GetJoinRequests.GetJoinRequestsQuery(id, page, pageSize);
        var requests = await _mediator.Send(query, cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{id}/join-requests/{requestId}/respond")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<JoinRequestDto>> RespondJoinRequest(Guid id, Guid requestId, [FromBody] RespondJoinRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Teams.Commands.RespondJoinRequest.RespondJoinRequestCommand(id, requestId, request.Approve, userId, userRole);
        var response = await _mediator.Send(command, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id}/invite")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<JoinRequestDto>> Invite(Guid id, AddPlayerRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var command = new Application.Features.Teams.Commands.InvitePlayer.InvitePlayerCommand(id, Guid.Parse(userId), request);
        var joinRequest = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, joinRequest);
    }

    [HttpPost("{id}/add-guest-player")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<PlayerDto>> AddGuestPlayer(Guid id, AddGuestPlayerRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var command = new Application.Features.Teams.Commands.AddGuestPlayer.AddGuestPlayerCommand(id, Guid.Parse(userId), request);
        var player = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, player);
    }

    [HttpGet("{id}/requests")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<JoinRequestDto>>> GetRequests(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Teams.Queries.GetJoinRequests.GetJoinRequestsQuery(id, page, pageSize);
        var requests = await _mediator.Send(query, cancellationToken);
        return Ok(requests);
    }

    [HttpDelete("{id}/players/{playerId}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<RemovePlayerResponse>> RemovePlayer(Guid id, Guid playerId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Teams.Commands.RemovePlayer.RemovePlayerCommand(id, playerId, userId, userRole);
        await _mediator.Send(command, cancellationToken);
        return Ok(new RemovePlayerResponse { TeamRemoved = true, PlayerId = playerId, TeamId = id });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Teams.Commands.DeleteTeam.DeleteTeamCommand(id, userId, userRole);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }

    [HttpPost("{id}/disable")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<MessageResponse>> Disable(Guid id, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Teams.Commands.DisableTeam.DisableTeamCommand(id);
        await _mediator.Send(command, cancellationToken);
        return Ok(new MessageResponse("Team disabled and withdrawn from all active tournaments."));
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<MessageResponse>> Activate(Guid id, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Teams.Commands.ActivateTeam.ActivateTeamCommand(id);
        await _mediator.Send(command, cancellationToken);
        return Ok(new MessageResponse("Team activated successfully."));
    }

    [HttpGet("{id}/players")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<PlayerDto>>> GetTeamPlayers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Teams.Queries.GetTeamPlayers.GetTeamPlayersQuery(id, page, pageSize);
        var players = await _mediator.Send(query, cancellationToken);
        return Ok(players);
    }

    [HttpGet("{id}/matches")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<Application.DTOs.Matches.MatchDto>>> GetTeamMatches(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Teams.Queries.GetTeamMatches.GetTeamMatchesQuery(id, page, pageSize);
        var matches = await _mediator.Send(query, cancellationToken);
        return Ok(matches);
    }

    [HttpGet("{id}/financials")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<Application.DTOs.Tournaments.TeamRegistrationDto>>> GetTeamFinancials(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Teams.Queries.GetTeamFinancials.GetTeamFinancialsQuery(id, page, pageSize);
        var financials = await _mediator.Send(query, cancellationToken);
        return Ok(financials);
    }
}
