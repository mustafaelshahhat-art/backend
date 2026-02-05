using Application.DTOs.Matches;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<ActionResult<IEnumerable<MatchDto>>> GetAll()
    {
        var matches = await _matchService.GetAllAsync();
        return Ok(matches);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<MatchDto>> GetById(Guid id)
    {
        var match = await _matchService.GetByIdAsync(id);
        if (match == null) return NotFound();
        return Ok(match);
    }

    [HttpPost("{id}/start")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult<MatchDto>> StartMatch(Guid id)
    {
        if (!await IsUserActiveAsync() && !User.IsInRole("Admin"))
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من إدارة المباريات.");
        }
        var match = await _matchService.StartMatchAsync(id);
        return Ok(match);
    }

    [HttpPost("{id}/end")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult<MatchDto>> EndMatch(Guid id)
    {
        if (!await IsUserActiveAsync() && !User.IsInRole("Admin"))
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من إدارة المباريات.");
        }
        var match = await _matchService.EndMatchAsync(id);
        return Ok(match);
    }

    [HttpPost("{id}/events")]
    [Authorize(Roles = "Referee,Admin")]
    public async Task<ActionResult<MatchDto>> AddEvent(Guid id, AddMatchEventRequest request)
    {
        if (!await IsUserActiveAsync() && !User.IsInRole("Admin"))
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من إضافة أحداث للمباراة.");
        }
        var match = await _matchService.AddEventAsync(id, request);
        return Ok(match);
    }

    [HttpPost("{id}/report")]
    [Authorize(Roles = "Referee")]
    public async Task<ActionResult<MatchDto>> SubmitReport(Guid id, SubmitReportRequest request)
    {
        if (!await IsUserActiveAsync() && !User.IsInRole("Admin"))
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من تقديم تقرير المباراة.");
        }
        var match = await _matchService.SubmitReportAsync(id, request);
        return Ok(match);
    }

    private async Task<bool> IsUserActiveAsync()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null) return false;

        var user = await _userService.GetByIdAsync(Guid.Parse(userIdClaim.Value));
        return user?.Status == "Active";
    }
}
