using Application.DTOs.Teams;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Application.DTOs.Tournaments;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamsController(ITeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TeamDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] Guid? captainId = null, [FromQuery] Guid? playerId = null, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var result = await _teamService.GetPagedAsync(page, pageSize, captainId, playerId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TeamDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var team = await _teamService.GetByIdAsync(id, cancellationToken);
        if (team == null) return NotFound();
        return Ok(team);
    }

    [HttpPost]
    public async Task<ActionResult<TeamDto>> Create(CreateTeamRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var team = await _teamService.CreateAsync(request, Guid.Parse(userId), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = team.Id }, team);
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<TeamDto>> Update(Guid id, UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var updatedTeam = await _teamService.UpdateAsync(id, request, userId, userRole, cancellationToken);
        return Ok(updatedTeam);
    }

    [HttpPost("{id}/join")]
    public async Task<ActionResult<JoinRequestDto>> RequestJoin(Guid id, CancellationToken cancellationToken) 
    {
        // Body might be empty or specific. Contract says POST /{id}/join.
        // Assuming user ID from token.
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var request = await _teamService.RequestJoinAsync(id, Guid.Parse(userId), cancellationToken);
        return Ok(request);
    }

    [HttpGet("{id}/join-requests")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<JoinRequestDto>>> GetJoinRequests(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var requests = await _teamService.GetJoinRequestsAsync(id, page, pageSize, cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{id}/join-requests/{requestId}/respond")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<JoinRequestDto>> RespondJoinRequest(Guid id, Guid requestId, [FromBody] RespondJoinRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var response = await _teamService.RespondJoinRequestAsync(id, requestId, request.Approve, userId, userRole, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{id}/invite")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<JoinRequestDto>> Invite(Guid id, AddPlayerRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var joinRequest = await _teamService.InvitePlayerAsync(id, Guid.Parse(userId), request, cancellationToken);
        return Ok(joinRequest);
    }

    [HttpPost("{id}/add-guest-player")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<PlayerDto>> AddGuestPlayer(Guid id, AddGuestPlayerRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var player = await _teamService.AddGuestPlayerAsync(id, Guid.Parse(userId), request, cancellationToken);
        return Ok(player);
    }

    [HttpGet("{id}/requests")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<JoinRequestDto>>> GetRequests(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var requests = await _teamService.GetJoinRequestsAsync(id, page, pageSize, cancellationToken);
        return Ok(requests);
    }

    [HttpDelete("{id}/players/{playerId}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<object>> RemovePlayer(Guid id, Guid playerId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        await _teamService.RemovePlayerAsync(id, playerId, userId, userRole, cancellationToken);
        return Ok(new { teamRemoved = true, playerId = playerId, teamId = id });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        await _teamService.DeleteAsync(id, userId, userRole, cancellationToken);
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
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        await _teamService.DisableTeamAsync(id, cancellationToken);
        return Ok(new { message = "Team disabled and withdrawn from all active tournaments." });
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await _teamService.ActivateTeamAsync(id, cancellationToken);
        return Ok(new { message = "Team activated successfully." });
    }

    [HttpGet("{id}/players")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<PlayerDto>>> GetTeamPlayers(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var players = await _teamService.GetTeamPlayersAsync(id, page, pageSize, cancellationToken);
        return Ok(players);
    }

    [HttpGet("{id}/matches")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<Application.DTOs.Matches.MatchDto>>> GetTeamMatches(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var matches = await _teamService.GetTeamMatchesAsync(id, page, pageSize, cancellationToken);
        return Ok(matches);
    }

    [HttpGet("{id}/financials")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<Application.DTOs.Tournaments.TeamRegistrationDto>>> GetTeamFinancials(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var financials = await _teamService.GetTeamFinancialsAsync(id, page, pageSize, cancellationToken);
        return Ok(financials);
    }
}
