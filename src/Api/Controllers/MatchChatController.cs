using Application.DTOs.Matches;
using Application.Features.MatchChat.Queries.GetChatHistory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/matches")] // extending matches route
public class MatchChatController : ControllerBase
{
    private readonly IMediator _mediator;

    public MatchChatController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}/chat")]
    [Authorize]
    [OutputCache(PolicyName = "ShortCache")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<MatchMessageDto>>> GetChatHistory(Guid id, [FromQuery] int pageSize = 50, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 200) pageSize = 200;
        var query = new GetChatHistoryQuery(id, pageSize, page);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
