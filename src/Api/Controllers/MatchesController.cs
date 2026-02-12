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
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> StartMatch(Guid id)
    {
        if (!await IsUserActiveAsync() && !User.IsInRole("Admin"))
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من إدارة المباريات.");
        }
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.StartMatchAsync(id, userId, userRole);
        return Ok(match);
    }

    [HttpPost("{id}/end")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> EndMatch(Guid id)
    {
        if (!await IsUserActiveAsync() && !User.IsInRole("Admin"))
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من إدارة المباريات.");
        }
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.EndMatchAsync(id, userId, userRole);
        return Ok(match);
    }

    [HttpPost("{id}/events")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> AddEvent(Guid id, AddMatchEventRequest request)
    {
        if (!await IsUserActiveAsync() && !User.IsInRole("Admin"))
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من إضافة أحداث للمباراة.");
        }
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.AddEventAsync(id, request, userId, userRole);
        return Ok(match);
    }

    [HttpDelete("{id}/events/{eventId}")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<MatchDto>> RemoveEvent(Guid id, Guid eventId)
    {
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.RemoveEventAsync(id, eventId, userId, userRole);
        return Ok(match);
    }



    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<MatchDto>> UpdateMatch(Guid id, UpdateMatchRequest request)
    {
        var (userId, userRole) = GetUserContext();
        var match = await _matchService.UpdateAsync(id, request, userId, userRole);
        return Ok(match);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        
        var role = User.IsInRole("Admin") ? "Admin" : "TournamentCreator";
        return (Guid.Parse(idStr!), role);
    }

    private async Task<bool> IsUserActiveAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
                  
        if (userId == null) return false;

        var user = await _userService.GetByIdAsync(Guid.Parse(userId));
        return user?.Status == "Active";
    }
}
