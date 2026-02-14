using Application.DTOs.Matches;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.Enums;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MatchesController : ControllerBase
{
    private readonly IMatchService _matchService;
    private readonly IUserService _userService;

    public MatchesController(IMatchService matchService, IUserService userService)
    {
        _matchService = matchService;
        _userService = userService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GetAll(CancellationToken cancellationToken)
    {
        var matches = await _matchService.GetAllAsync(cancellationToken);
        return Ok(matches);
    }



    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<MatchDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var match = await _matchService.GetByIdAsync(id, cancellationToken);
        if (match == null) return NotFound();
        return Ok(match);
    }

    [HttpPost("{id}/start")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> StartMatch(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.StartMatchAsync(id, userId, userRole, cancellationToken);
        return Ok(match);
    }

    [HttpPost("{id}/end")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> EndMatch(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.EndMatchAsync(id, userId, userRole, cancellationToken);
        return Ok(match);
    }

    [HttpPost("{id}/events")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> AddEvent(Guid id, AddMatchEventRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.AddEventAsync(id, request, userId, userRole, cancellationToken);
        return Ok(match);
    }

    [HttpDelete("{id}/events/{eventId}")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> RemoveEvent(Guid id, Guid eventId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.RemoveEventAsync(id, eventId, userId, userRole, cancellationToken);
        return Ok(match);
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<MatchDto>> UpdateMatch(Guid id, UpdateMatchRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.UpdateAsync(id, request, userId, userRole, cancellationToken);
        return Ok(match);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }

    private async Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(userId, cancellationToken);
        return user?.Status == UserStatus.Active.ToString();
    }
}
