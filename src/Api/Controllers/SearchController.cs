using Application.Common.Models;
using Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly IMediator _mediator;

    public SearchController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [OutputCache(PolicyName = "SearchResults")]
    public async Task<ActionResult<PagedResult<SearchResultItem>>> Search([FromQuery] string q, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(new PagedResult<SearchResultItem>(new List<SearchResultItem>(), 0, page, pageSize));
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Player";

        var query = new Application.Features.Search.Queries.SearchQuery(q, page, pageSize, userId, role);
        var results = await _mediator.Send(query, cancellationToken);
        return Ok(results);
    }
}
