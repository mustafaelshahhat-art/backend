using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(new { results = Array.Empty<object>(), totalCount = 0 });
        }

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Player";

        var results = await _searchService.SearchAsync(q, userId, role, cancellationToken);
        return Ok(results);
    }
}
