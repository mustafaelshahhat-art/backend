using Application.DTOs.Teams;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly ITeamService _teamService;
    private readonly IUserService _userService;

    public MeController(ITeamService teamService, IUserService userService)
    {
        _teamService = teamService;
        _userService = userService;
    }

    [HttpGet("teams-overview")]
    public async Task<ActionResult<TeamsOverviewDto>> GetTeamsOverview()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var overview = await _teamService.GetTeamsOverviewAsync(Guid.Parse(userId));
        return Ok(overview);
    }
}