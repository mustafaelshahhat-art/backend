using Application.DTOs.Matches;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Domain.Enums;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MatchesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IOutputCacheStore _cacheStore;

    public MatchesController(IMediator mediator, IOutputCacheStore cacheStore)
    {
        _mediator = mediator;
        _cacheStore = cacheStore;
    }

    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = "MatchList")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<MatchDto>>> GetAll(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] Guid? creatorId = null, 
        [FromQuery] string? status = null,
        [FromQuery] Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Matches.Queries.GetMatchesPaged.GetMatchesPagedQuery(page, pageSize, creatorId, status, teamId);
        var matches = await _mediator.Send(query, cancellationToken);
        return Ok(matches);
    }



    [HttpGet("{id}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = "MatchDetail")]
    public async Task<ActionResult<MatchDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new Application.Features.Matches.Queries.GetMatchById.GetMatchByIdQuery(id);
        var match = await _mediator.Send(query, cancellationToken);
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
        await _cacheStore.EvictByTagAsync("matches", cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/end")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> EndMatch(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Matches.Commands.EndMatch.EndMatchCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        // Evict matches + standings — score finalization affects both
        await _cacheStore.EvictByTagAsync("matches", cancellationToken);
        await _cacheStore.EvictByTagAsync("standings", cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/events")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> AddEvent(Guid id, [FromBody] AddMatchEventRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Matches.Commands.AddMatchEvent.AddMatchEventCommand(id, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        await _cacheStore.EvictByTagAsync("matches", cancellationToken);
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
    [Authorize(Policy = "RequireCreator")]
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
}
