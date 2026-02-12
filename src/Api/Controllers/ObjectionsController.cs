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
    public async Task<ActionResult<IEnumerable<ObjectionDto>>> GetAll()
    {
        if (User.HasClaim(c => c.Type == ClaimTypes.Role && (c.Value == "Admin" || c.Value == "TournamentCreator")))
        {
            var isCreator = User.IsInRole("TournamentCreator");
            Guid? creatorId = isCreator ? Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!) : null;
            var objections = await _objectionService.GetAllAsync(creatorId);
            return Ok(objections);
        }
        else
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                        ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            
            if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();
            
            var userId = Guid.Parse(userIdStr);
            var user = await _userService.GetByIdAsync(userId);
            
            if (user == null || !user.TeamId.HasValue)
            {
                return Ok(new List<ObjectionDto>());
            }

            var objections = await _objectionService.GetByTeamIdAsync(user.TeamId.Value);
            return Ok(objections);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ObjectionDto>> GetById(Guid id)
    {
        var (userId, userRole) = GetUserContext();
        var objection = await _objectionService.GetByIdAsync(id, userId, userRole);
        if (objection == null) return NotFound();
        
        return Ok(objection);
    }

    [HttpPost]
    [Authorize(Policy = "RequireTeamCaptain")]
    public async Task<ActionResult<ObjectionDto>> Submit(SubmitObjectionRequest request)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                  ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        
        if (userIdStr == null) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var user = await _userService.GetByIdAsync(userId);
        
        if (user == null || !user.TeamId.HasValue)
        {
            return BadRequest("يجب أن تكون عضواً في فريق لتقديم اعتراض.");
        }
        
        var objection = await _objectionService.SubmitAsync(request, user.TeamId.Value);
        return CreatedAtAction(nameof(GetById), new { id = objection.Id }, objection);
    }

    [HttpPost("{id}/resolve")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<ObjectionDto>> Resolve(Guid id, ResolveObjectionRequest request)
    {
        var (userId, userRole) = GetUserContext();
        var objectionResult = await _objectionService.ResolveAsync(id, request, userId, userRole);
        return Ok(objectionResult);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.IsInRole("Admin") ? "Admin" : "TournamentCreator";
        return (Guid.Parse(idStr!), role);
    }
}
