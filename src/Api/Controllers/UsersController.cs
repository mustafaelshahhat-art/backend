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

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    [HttpGet("role/{role}")]
    [Authorize]
    public async Task<ActionResult> GetByRole(string role)
    {
        var (userId, userRole) = GetUserContext();
        
        if (userRole == UserRole.Admin.ToString())
        {
            var users = await _userService.GetByRoleAsync(role);
            return Ok(users);
        }
        
        // Non-admins get public/restricted view
        var publicUsers = await _userService.GetPublicByRoleAsync(role);
        return Ok(publicUsers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetById(Guid id)
    {
        var (userId, userRole) = GetUserContext();

        if (userId == id || userRole == UserRole.Admin.ToString())
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null) return NotFound();
            return Ok(user);
        }

        // Public view
        var publicUser = await _userService.GetPublicByIdAsync(id);
        if (publicUser == null) return NotFound();
        return Ok(publicUser);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request)
    {
        var (userId, userRole) = GetUserContext();

        if (userId != id && userRole != UserRole.Admin.ToString())
        {
            return Forbid();
        }

        var updatedUser = await _userService.UpdateAsync(id, request);
        return Ok(updatedUser);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _userService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/suspend")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        await _userService.SuspendAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [Authorize(Policy = "RequireAdmin")]
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
    [Authorize(Policy = "RequireAdmin")]
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
    /// Creates a new tournament creator user. Only accessible by existing admins.
    /// Role is forced to TournamentCreator on the backend.
    /// </summary>
    [HttpPost("create-tournament-creator")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<UserDto>> CreateTournamentCreator(CreateAdminRequest request)
    {
        var creatorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(creatorId) || !Guid.TryParse(creatorId, out var adminId))
        {
            return Unauthorized();
        }

        var newCreator = await _userService.CreateTournamentCreatorAsync(request, adminId);
        return Ok(newCreator);
    }



    /// <summary>
    /// Gets the count of active admins. Used for safety checks on the frontend.
    /// </summary>
    [HttpGet("admin-count")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<AdminCountDto>> GetAdminCount([FromQuery] Guid? userId = null)
    {
        var result = await _userService.GetAdminCountAsync(userId);
        return Ok(result);
    }

    /// <summary>
    /// Changes the password for the current authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        try
        {
            await _userService.ChangePasswordAsync(id, request.CurrentPassword, request.NewPassword);
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
    public async Task<ActionResult<string>> UploadAvatar(UploadAvatarRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Base64Image))
        {
            return BadRequest("Image data is required");
        }

        // Validate base64 image
        var base64Data = request.Base64Image;
        if (base64Data.StartsWith("data:image"))
        {
            base64Data = base64Data.Substring(base64Data.IndexOf(",") + 1);
        }

        try
        {
            var imageBytes = Convert.FromBase64String(base64Data);
            
            // Validate file size (max 2MB)
            if (imageBytes.Length > 2 * 1024 * 1024)
            {
                return BadRequest("File size must be less than 2MB");
            }
        }
        catch
        {
            return BadRequest("Invalid image data");
        }

        try
        {
            var avatarUrl = await _userService.UploadAvatarAsync(id, request);
            return Ok(new { avatarUrl });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }
}

