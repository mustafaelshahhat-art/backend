using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class LocationsController : ControllerBase
{
    private readonly IUserService _userService;

    public LocationsController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("governorates")]
    public async Task<ActionResult<IEnumerable<string>>> GetGovernorates()
    {
        var result = await _userService.GetGovernoratesAsync();
        return Ok(result);
    }

    [HttpGet("cities")]
    public async Task<ActionResult<IEnumerable<string>>> GetCities([FromQuery] string governorateId)
    {
        var result = await _userService.GetCitiesAsync(governorateId);
        return Ok(result);
    }

    [HttpGet("districts")]
    public async Task<ActionResult<IEnumerable<string>>> GetDistricts([FromQuery] string cityId)
    {
        var result = await _userService.GetDistrictsAsync(cityId);
        return Ok(result);
    }
}
