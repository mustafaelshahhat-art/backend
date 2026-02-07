using Application.DTOs.Users;
using Application.Interfaces;
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

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    [HttpGet("role/{role}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetByRole(string role)
    {
        var users = await _userService.GetByRoleAsync(role);
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(Guid id)
    {
        var user = await _userService.GetByIdAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request)
    {
        // Owner Check
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        if (userId != id.ToString() && userRole != "Admin")
        {
            return Forbid();
        }

        var updatedUser = await _userService.UpdateAsync(id, request);
        return Ok(updatedUser);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _userService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/suspend")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        await _userService.SuspendAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _userService.ActivateAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Creates a new admin user. Only accessible by existing admins.
    /// Role is forced to Admin on the backend.
    /// </summary>
    [HttpPost("create-admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> CreateAdmin(CreateAdminRequest request)
    {
        var creatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(creatorId) || !Guid.TryParse(creatorId, out var adminId))
        {
            return Unauthorized();
        }

        var newAdmin = await _userService.CreateAdminAsync(request, adminId);
        return Ok(newAdmin);
    }

    /// <summary>
    /// Gets the count of active admins. Used for safety checks on the frontend.
    /// </summary>
    [HttpGet("admin-count")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AdminCountDto>> GetAdminCount([FromQuery] Guid? userId = null)
    {
        var result = await _userService.GetAdminCountAsync(userId);
        return Ok(result);
    }
}

