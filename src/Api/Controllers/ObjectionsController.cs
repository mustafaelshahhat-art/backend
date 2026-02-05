using Application.DTOs.Objections;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ObjectionsController : ControllerBase
{
    private readonly IObjectionService _objectionService;

    public ObjectionsController(IObjectionService objectionService)
    {
        _objectionService = objectionService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<ObjectionDto>>> GetAll()
    {
        var objections = await _objectionService.GetAllAsync();
        return Ok(objections);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ObjectionDto>> GetById(Guid id)
    {
        var objection = await _objectionService.GetByIdAsync(id);
        if (objection == null) return NotFound();
        // Check permission?
        return Ok(objection);
    }

    [HttpPost]
    public async Task<ActionResult<ObjectionDto>> Submit(SubmitObjectionRequest request)
    {
        // Implementation TODO
        return await Task.FromResult(StatusCode(501, "Objection submission requires TeamId resolution logic."));
    }

    [HttpPost("{id}/resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ObjectionDto>> Resolve(Guid id, ResolveObjectionRequest request)
    {
        var objection = await _objectionService.ResolveAsync(id, request);
        return Ok(objection);
    }
}
