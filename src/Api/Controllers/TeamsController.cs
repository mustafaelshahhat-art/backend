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
    public async Task<ActionResult<IEnumerable<TeamDto>>> GetAll()
    {
        var teams = await _teamService.GetAllAsync();
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
    public async Task<ActionResult<TeamDto>> Update(Guid id, UpdateTeamRequest request)
    {
        // Owner Check (Captain)
        // Need to check if user is captain of this team. Service check or here?
        // Ideally service handles permission or we fetch team here.
        var team = await _teamService.GetByIdAsync(id);
        if (team == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (team.CaptainId.ToString() != userId && !User.IsInRole("Admin"))
        {
            return Forbid();
        }

        var updatedTeam = await _teamService.UpdateAsync(id, request);
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
    public async Task<ActionResult<IEnumerable<JoinRequestDto>>> GetJoinRequests(Guid id)
    {
        // Check captain
        var team = await _teamService.GetByIdAsync(id);
        if (team == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (team.CaptainId.ToString() != userId && !User.IsInRole("Admin")) return Forbid();

        var requests = await _teamService.GetJoinRequestsAsync(id);
        return Ok(requests);
    }

    [HttpPost("{id}/join-requests/{requestId}/respond")]
    public async Task<ActionResult<JoinRequestDto>> RespondJoinRequest(Guid id, Guid requestId, [FromBody] RespondJoinRequest request)
    {
        // Check captain
        var team = await _teamService.GetByIdAsync(id);
        if (team == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (team.CaptainId.ToString() != userId && !User.IsInRole("Admin")) return Forbid();

        var response = await _teamService.RespondJoinRequestAsync(id, requestId, request.Approve);
        return Ok(response);
    }

    [HttpPost("{id}/players")]
    public async Task<ActionResult<PlayerDto>> AddPlayer(Guid id, AddPlayerRequest request)
    {
        // Check captain
        var team = await _teamService.GetByIdAsync(id);
        if (team == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (team.CaptainId.ToString() != userId && !User.IsInRole("Admin")) return Forbid();

        var player = await _teamService.AddPlayerAsync(id, request);
        return Ok(player);
    }

    [HttpDelete("{id}/players/{playerId}")]
    public async Task<IActionResult> RemovePlayer(Guid id, Guid playerId)
    {
        // Check captain
        var team = await _teamService.GetByIdAsync(id);
        if (team == null) return NotFound();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (team.CaptainId.ToString() != userId && !User.IsInRole("Admin")) return Forbid();

        await _teamService.RemovePlayerAsync(id, playerId);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _teamService.DeleteAsync(id);
        return NoContent();
    }
}
