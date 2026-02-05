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
    private readonly IUserService _userService;

    public ObjectionsController(IObjectionService objectionService, IUserService userService)
    {
        _objectionService = objectionService;
        _userService = userService;
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
    [Authorize(Roles = "Captain")]
    public async Task<ActionResult<ObjectionDto>> Submit(SubmitObjectionRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        
        if (userId == null) return Unauthorized();

        var user = await _userService.GetByIdAsync(Guid.Parse(userId));
        if (user == null || !user.TeamId.HasValue)
        {
            return BadRequest("يجب أن تكون كابتن فريق لتقديم اعتراض.");
        }

        var objection = await _objectionService.SubmitAsync(request, user.TeamId.Value);
        return CreatedAtAction(nameof(GetById), new { id = objection.Id }, objection);
    }

    [HttpPost("{id}/resolve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ObjectionDto>> Resolve(Guid id, ResolveObjectionRequest request)
    {
        var objection = await _objectionService.ResolveAsync(id, request);
        return Ok(objection);
    }
}
