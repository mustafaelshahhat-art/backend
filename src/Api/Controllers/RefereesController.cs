using Application.DTOs.Users;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class RefereesController : ControllerBase
{
    private readonly IUserService _userService;

    public RefereesController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetReferees(
        [FromQuery] string? districtId = null, 
        [FromQuery] string? cityId = null, 
        [FromQuery] string? governorateId = null)
    {
        var result = await _userService.GetRefereesByLocationAsync(districtId, cityId, governorateId);
        return Ok(result);
    }
}
