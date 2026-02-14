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
    public async Task<ActionResult<IEnumerable<string>>> GetGovernorates(CancellationToken cancellationToken)
    {
        var result = await _userService.GetGovernoratesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("cities")]
    public async Task<ActionResult<IEnumerable<string>>> GetCities([FromQuery] string governorateId, CancellationToken cancellationToken)
    {
        var result = await _userService.GetCitiesAsync(governorateId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("districts")]
    public async Task<ActionResult<IEnumerable<string>>> GetDistricts([FromQuery] string cityId, CancellationToken cancellationToken)
    {
        var result = await _userService.GetDistrictsAsync(cityId, cancellationToken);
        return Ok(result);
    }
}
