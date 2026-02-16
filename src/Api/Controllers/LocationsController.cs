using Application.DTOs.Locations;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Public location endpoints for cascading dropdowns (registration / profile).
/// No auth required — lightweight cached responses.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationsController(ILocationService locationService)
    {
        _locationService = locationService;
    }

    [HttpGet("governorates")]
    [ResponseCache(Duration = 300)] // 5-min HTTP cache
    public async Task<ActionResult<IReadOnlyList<GovernorateDto>>> GetGovernorates(CancellationToken ct)
    {
        var result = await _locationService.GetGovernoratesAsync(ct);
        return Ok(result);
    }

    [HttpGet("cities")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "governorateId" })]
    public async Task<ActionResult<IReadOnlyList<CityDto>>> GetCities([FromQuery] Guid governorateId, CancellationToken ct)
    {
        var result = await _locationService.GetCitiesByGovernorateAsync(governorateId, ct);
        return Ok(result);
    }

    [HttpGet("areas")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "cityId" })]
    public async Task<ActionResult<IReadOnlyList<AreaDto>>> GetAreas([FromQuery] Guid cityId, CancellationToken ct)
    {
        var result = await _locationService.GetAreasByCityAsync(cityId, ct);
        return Ok(result);
    }
}

