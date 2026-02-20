using Application.DTOs.Users;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<UserDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Users.Queries.GetUsersPaged.GetUsersPagedQuery(page, pageSize, null);
        var users = await _mediator.Send(query, cancellationToken);
        return Ok(users);
    }

    [HttpGet("role/{role}")]
    [Authorize]
    [ProducesResponseType(typeof(Application.Common.Models.PagedResult<UserDto>), 200)]
    [ProducesResponseType(typeof(Application.Common.Models.PagedResult<UserPublicDto>), 200)]
    public async Task<IActionResult> GetByRole(string role, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var (userId, userRole) = GetUserContext();
        
        if (userRole == UserRole.Admin.ToString())
        {
            var query = new Application.Features.Users.Queries.GetUsersPaged.GetUsersPagedQuery(page, pageSize, role);
            var users = await _mediator.Send(query, cancellationToken);
            return Ok(users);
        }
        
        // Non-admins get public/restricted view
        var publicQuery = new Application.Features.Users.Queries.GetPublicUsersPaged.GetPublicUsersPagedQuery(page, pageSize, role);
        var publicUsers = await _mediator.Send(publicQuery, cancellationToken);
        return Ok(publicUsers);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(typeof(UserPublicDto), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();

        if (userId == id || userRole == UserRole.Admin.ToString())
        {
            var query = new Application.Features.Users.Queries.GetUserById.GetUserByIdQuery(id);
            var user = await _mediator.Send(query, cancellationToken);
            if (user == null) return NotFound();
            return Ok(user);
        }

        // Public view
        var publicQuery = new Application.Features.Users.Queries.GetPublicUserById.GetPublicUserByIdQuery(id);
        var publicUser = await _mediator.Send(publicQuery, cancellationToken);
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

        var command = new Application.Features.Users.Commands.UpdateUser.UpdateUserCommand(id, request);
        var updatedUser = await _mediator.Send(command, cancellationToken);
        return Ok(updatedUser);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Users.Commands.DeleteUser.DeleteUserCommand(id);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/suspend")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Users.Commands.SuspendUser.SuspendUserCommand(id);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Users.Commands.ActivateUser.ActivateUserCommand(id);
        await _mediator.Send(command, cancellationToken);
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

        var command = new Application.Features.Users.Commands.CreateAdmin.CreateAdminCommand(request, adminId);
        var newAdmin = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, newAdmin);
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

        var command = new Application.Features.Users.Commands.CreateTournamentCreator.CreateTournamentCreatorCommand(request, adminId);
        var newCreator = await _mediator.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, newCreator);
    }



    /// <summary>
    /// Gets the count of active admins. Used for safety checks on the frontend.
    /// </summary>
    [HttpGet("admin-count")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<AdminCountDto>> GetAdminCount([FromQuery] Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = new Application.Features.Users.Queries.GetAdminCount.GetAdminCountQuery(userId);
        var result = await _mediator.Send(query, cancellationToken);
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

        var command = new Application.Features.Users.Commands.ChangePassword.ChangePasswordCommand(id, request.CurrentPassword, request.NewPassword);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }
}

