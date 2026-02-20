using Application.Common.Models;
using Application.DTOs.Locations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Admin-only location management endpoints.
/// </summary>
[ApiController]
[Route("api/v1/admin/locations")]
[Authorize(Policy = "RequireAdmin")]
public class AdminLocationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminLocationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ── Governorates ──

    [HttpGet("governorates")]
    public async Task<ActionResult<PagedResult<GovernorateAdminDto>>> GetGovernorates(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Queries.GetGovernoratesPaged.GetGovernoratesPagedQuery(page, pageSize, search, isActive), ct);
        return Ok(result);
    }

    [HttpPost("governorate")]
    public async Task<ActionResult<GovernorateAdminDto>> CreateGovernorate(
        [FromBody] CreateGovernorateRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Commands.CreateGovernorate.CreateGovernorateCommand(request), ct);
        return CreatedAtAction(nameof(GetGovernorates), new { id = result.Id }, result);
    }

    [HttpPut("governorate/{id}")]
    public async Task<ActionResult<GovernorateAdminDto>> UpdateGovernorate(
        Guid id, [FromBody] UpdateLocationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Commands.UpdateGovernorate.UpdateGovernorateCommand(id, request), ct);
        return Ok(result);
    }

    [HttpPut("governorate/{id}/activate")]
    public async Task<IActionResult> ActivateGovernorate(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new Application.Features.Locations.Commands.ActivateGovernorate.ActivateGovernorateCommand(id), ct);
        return NoContent();
    }

    [HttpPut("governorate/{id}/deactivate")]
    public async Task<IActionResult> DeactivateGovernorate(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new Application.Features.Locations.Commands.DeactivateGovernorate.DeactivateGovernorateCommand(id), ct);
        return NoContent();
    }

    // ── Cities ──

    [HttpGet("cities")]
    public async Task<ActionResult<PagedResult<CityAdminDto>>> GetCities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? governorateId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Queries.GetCitiesPaged.GetCitiesPagedQuery(page, pageSize, governorateId, search, isActive), ct);
        return Ok(result);
    }

    [HttpPost("city")]
    public async Task<ActionResult<CityAdminDto>> CreateCity(
        [FromBody] CreateCityRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Commands.CreateCity.CreateCityCommand(request), ct);
        return CreatedAtAction(nameof(GetCities), new { id = result.Id }, result);
    }

    [HttpPut("city/{id}")]
    public async Task<ActionResult<CityAdminDto>> UpdateCity(
        Guid id, [FromBody] UpdateLocationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Commands.UpdateCity.UpdateCityCommand(id, request), ct);
        return Ok(result);
    }

    [HttpPut("city/{id}/activate")]
    public async Task<IActionResult> ActivateCity(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new Application.Features.Locations.Commands.ActivateCity.ActivateCityCommand(id), ct);
        return NoContent();
    }

    [HttpPut("city/{id}/deactivate")]
    public async Task<IActionResult> DeactivateCity(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new Application.Features.Locations.Commands.DeactivateCity.DeactivateCityCommand(id), ct);
        return NoContent();
    }

    // ── Areas ──

    [HttpGet("areas")]
    public async Task<ActionResult<PagedResult<AreaAdminDto>>> GetAreas(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? cityId = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Queries.GetAreasPaged.GetAreasPagedQuery(page, pageSize, cityId, search, isActive), ct);
        return Ok(result);
    }

    [HttpPost("area")]
    public async Task<ActionResult<AreaAdminDto>> CreateArea(
        [FromBody] CreateAreaRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Commands.CreateArea.CreateAreaCommand(request), ct);
        return CreatedAtAction(nameof(GetAreas), new { id = result.Id }, result);
    }

    [HttpPut("area/{id}")]
    public async Task<ActionResult<AreaAdminDto>> UpdateArea(
        Guid id, [FromBody] UpdateLocationRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new Application.Features.Locations.Commands.UpdateArea.UpdateAreaCommand(id, request), ct);
        return Ok(result);
    }

    [HttpPut("area/{id}/activate")]
    public async Task<IActionResult> ActivateArea(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new Application.Features.Locations.Commands.ActivateArea.ActivateAreaCommand(id), ct);
        return NoContent();
    }

    [HttpPut("area/{id}/deactivate")]
    public async Task<IActionResult> DeactivateArea(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new Application.Features.Locations.Commands.DeactivateArea.DeactivateAreaCommand(id), ct);
        return NoContent();
    }
}
