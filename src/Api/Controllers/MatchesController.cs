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
    private readonly MediatR.IMediator _mediator;

    public MatchesController(IMatchService matchService, IUserService userService, MediatR.IMediator mediator)
    {
        _matchService = matchService;
        _userService = userService;
        _mediator = mediator;
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(Duration = 15, VaryByQueryKeys = new[] { "page", "pageSize", "creatorId", "status", "teamId" })]
    public async Task<ActionResult<Application.Common.Models.PagedResult<MatchDto>>> GetAll(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] Guid? creatorId = null, 
        [FromQuery] string? status = null,
        [FromQuery] Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var matches = await _matchService.GetPagedAsync(page, pageSize, creatorId, status, teamId, cancellationToken);
        return Ok(matches);
    }



    [HttpGet("{id}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 10, VaryByQueryKeys = new[] { "id" })]
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
        var command = new Application.Features.Matches.Commands.StartMatch.StartMatchCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/end")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> EndMatch(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Matches.Commands.EndMatch.EndMatchCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/events")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> AddEvent(Guid id, [FromBody] AddMatchEventRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Matches.Commands.AddMatchEvent.AddMatchEventCommand(id, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}/events/{eventId}")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> RemoveEvent(Guid id, Guid eventId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Matches.Commands.RemoveMatchEvent.RemoveMatchEventCommand(id, eventId, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireCreator")] // Changed policy based on instruction's new line
    public async Task<ActionResult<MatchDto>> UpdateMatch(Guid id, [FromBody] UpdateMatchRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Matches.Commands.UpdateMatch.UpdateMatchCommand(id, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
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
