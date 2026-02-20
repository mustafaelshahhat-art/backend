using Application.Contracts.Settings.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Public status endpoints — no authentication required.
/// Separated from admin controllers so pre-auth pages (login, landing) can safely call them.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IMediator _mediator;

    public StatusController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Public endpoint to check if maintenance mode is enabled.
    /// Called by frontend login/landing pages before authentication.
    /// </summary>
    [HttpGet("maintenance")]
    public async Task<ActionResult<MaintenanceStatusResponse>> GetMaintenanceStatus(CancellationToken cancellationToken)
    {
        var isMaintenanceMode = await _mediator.Send(new Application.Features.SystemSettings.Queries.GetMaintenanceStatus.GetMaintenanceStatusQuery(), cancellationToken);
        return Ok(new MaintenanceStatusResponse { MaintenanceMode = isMaintenanceMode });
    }
}
