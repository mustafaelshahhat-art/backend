using Application.DTOs.Users;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly MediatR.IMediator _mediator;

    public UsersController(IUserService userService, MediatR.IMediator mediator)
    {
        _userService = userService;
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<UserDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var users = await _userService.GetPagedAsync(page, pageSize, null, cancellationToken);
        return Ok(users);
    }

    [HttpGet("role/{role}")]
    [Authorize]
    public async Task<ActionResult> GetByRole(string role, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var (userId, userRole) = GetUserContext();
        
        if (userRole == UserRole.Admin.ToString())
        {
            var users = await _userService.GetPagedAsync(page, pageSize, role, cancellationToken);
            return Ok(users);
        }
        
        // Non-admins get public/restricted view
        var publicUsers = await _userService.GetPublicPagedAsync(page, pageSize, role, cancellationToken);
        return Ok(publicUsers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();

        if (userId == id || userRole == UserRole.Admin.ToString())
        {
            var user = await _userService.GetByIdAsync(id, cancellationToken);
            if (user == null) return NotFound();
            return Ok(user);
        }

        // Public view
        var publicUser = await _userService.GetPublicByIdAsync(id, cancellationToken);
        if (publicUser == null) return NotFound();
        return Ok(publicUser);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();

        if (userId != id && userRole != UserRole.Admin.ToString())
        {
            return Forbid();
        }

        var updatedUser = await _userService.UpdateAsync(id, request, cancellationToken);
        return Ok(updatedUser);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/suspend")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        await _userService.SuspendAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        await _userService.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Creates a new admin user. Only accessible by existing admins.
    /// Role is forced to Admin on the backend.
    /// </summary>
    [HttpPost("create-admin")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<UserDto>> CreateAdmin(CreateAdminRequest request, CancellationToken cancellationToken)
    {
        var creatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(creatorId) || !Guid.TryParse(creatorId, out var adminId))
        {
            return Unauthorized();
        }

        var newAdmin = await _userService.CreateAdminAsync(request, adminId, cancellationToken);
        return Ok(newAdmin);
    }

    /// <summary>
    /// Creates a new tournament creator user. Only accessible by existing admins.
    /// Role is forced to TournamentCreator on the backend.
    /// </summary>
    [HttpPost("create-tournament-creator")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<UserDto>> CreateTournamentCreator(CreateAdminRequest request, CancellationToken cancellationToken)
    {
        var creatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(creatorId) || !Guid.TryParse(creatorId, out var adminId))
        {
            return Unauthorized();
        }

        var newCreator = await _userService.CreateTournamentCreatorAsync(request, adminId, cancellationToken);
        return Ok(newCreator);
    }



    /// <summary>
    /// Gets the count of active admins. Used for safety checks on the frontend.
    /// </summary>
    [HttpGet("admin-count")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<AdminCountDto>> GetAdminCount([FromQuery] Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var result = await _userService.GetAdminCountAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Changes the password for the current authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        try
        {
            await _userService.ChangePasswordAsync(id, request.CurrentPassword, request.NewPassword, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Uploads and updates the avatar for the current authenticated user.
    /// </summary>
    [HttpPost("upload-avatar")]
    public async Task<ActionResult<string>> UploadAvatar(UploadAvatarRequest request, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var command = new Application.Features.Users.Commands.UploadAvatar.UploadAvatarCommand(id, request.Base64Image, request.FileName);
        var avatarUrl = await _mediator.Send(command, cancellationToken);
        
        return Ok(new { avatarUrl });
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }
}

