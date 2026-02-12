using Application.DTOs.Teams;
using Application.Interfaces;
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
    public async Task<ActionResult<IEnumerable<TeamDto>>> GetAll([FromQuery] Guid? captainId, [FromQuery] Guid? playerId)
    {
        var teams = await _teamService.GetAllAsync(captainId, playerId);
        return Ok(teams);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TeamDto>> GetById(Guid id)
    {
        var team = await _teamService.GetByIdAsync(id);
        if (team == null) return NotFound();
        return Ok(team);
    }

    [HttpPost]
    public async Task<ActionResult<TeamDto>> Create(CreateTeamRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var team = await _teamService.CreateAsync(request, Guid.Parse(userId));
        return CreatedAtAction(nameof(GetById), new { id = team.Id }, team);
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<TeamDto>> Update(Guid id, UpdateTeamRequest request)
    {
        var (userId, userRole) = GetUserContext();
        var updatedTeam = await _teamService.UpdateAsync(id, request, userId, userRole);
        return Ok(updatedTeam);
    }

    [HttpPost("{id}/join")]
    public async Task<ActionResult<JoinRequestDto>> RequestJoin(Guid id) 
    {
        // Body might be empty or specific. Contract says POST /{id}/join.
        // Assuming user ID from token.
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var request = await _teamService.RequestJoinAsync(id, Guid.Parse(userId));
        return Ok(request);
    }

    [HttpGet("{id}/join-requests")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<IEnumerable<JoinRequestDto>>> GetJoinRequests(Guid id)
    {
        var requests = await _teamService.GetJoinRequestsAsync(id);
        return Ok(requests);
    }

    [HttpPost("{id}/join-requests/{requestId}/respond")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<JoinRequestDto>> RespondJoinRequest(Guid id, Guid requestId, [FromBody] RespondJoinRequest request)
    {
        var (userId, userRole) = GetUserContext();
        var response = await _teamService.RespondJoinRequestAsync(id, requestId, request.Approve, userId, userRole);
        return Ok(response);
    }

    [HttpPost("{id}/invite")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<JoinRequestDto>> Invite(Guid id, AddPlayerRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var joinRequest = await _teamService.InvitePlayerAsync(id, Guid.Parse(userId), request);
        return Ok(joinRequest);
    }

    [HttpGet("{id}/requests")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<IEnumerable<JoinRequestDto>>> GetRequests(Guid id)
    {
        var requests = await _teamService.GetJoinRequestsAsync(id);
        return Ok(requests);
    }

    [HttpDelete("{id}/players/{playerId}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<object>> RemovePlayer(Guid id, Guid playerId)
    {
        var (userId, userRole) = GetUserContext();
        await _teamService.RemovePlayerAsync(id, playerId, userId, userRole);
        return Ok(new { teamRemoved = true, playerId = playerId, teamId = id });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (userId, userRole) = GetUserContext();
        await _teamService.DeleteAsync(id, userId, userRole);
        return NoContent();
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.IsInRole("Admin") ? "Admin" : 
                   User.IsInRole("TournamentCreator") ? "TournamentCreator" : "User";
        return (Guid.Parse(idStr!), role);
    }

    [HttpPost("{id}/disable")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Disable(Guid id)
    {
        await _teamService.DisableTeamAsync(id);
        return Ok(new { message = "Team disabled and withdrawn from all active tournaments." });
    }

    [HttpGet("{id}/players")]
    public async Task<ActionResult<IEnumerable<PlayerDto>>> GetTeamPlayers(Guid id)
    {
        var players = await _teamService.GetTeamPlayersAsync(id);
        return Ok(players);
    }

    [HttpGet("{id}/matches")]
    public async Task<ActionResult<IEnumerable<Application.DTOs.Matches.MatchDto>>> GetTeamMatches(Guid id)
    {
        var matches = await _teamService.GetTeamMatchesAsync(id);
        return Ok(matches);
    }

    [HttpGet("{id}/financials")]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<IEnumerable<Application.DTOs.Tournaments.TeamRegistrationDto>>> GetTeamFinancials(Guid id)
    {
        var financials = await _teamService.GetTeamFinancialsAsync(id);
        return Ok(financials);
    }
}
