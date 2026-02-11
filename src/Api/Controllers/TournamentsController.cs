using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _tournamentService;
    private readonly IUserService _userService;

    public TournamentsController(ITournamentService tournamentService, IUserService userService)
    {
        _tournamentService = tournamentService;
        _userService = userService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TournamentDto>>> GetAll()
    {
        Guid? creatorId = null;
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("TournamentCreator"))
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdStr))
            {
                creatorId = Guid.Parse(userIdStr);
            }
        }

        var tournaments = await _tournamentService.GetAllAsync(creatorId);
        return Ok(tournaments);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentDto>> GetById(Guid id)
    {
        var tournament = await _tournamentService.GetByIdAsync(id);
        if (tournament == null) return NotFound();
        return Ok(tournament);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<TournamentDto>> Create(CreateTournamentRequest request)
    {
        Guid? creatorId = null;
        if (User.IsInRole("TournamentCreator"))
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdStr))
            {
                creatorId = Guid.Parse(userIdStr);
            }
        }
        
        var tournament = await _tournamentService.CreateAsync(request, creatorId);
        return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, tournament);
    }

    [HttpPatch("{id}")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<TournamentDto>> Update(Guid id, UpdateTournamentRequest request)
    {
        if (User.IsInRole("TournamentCreator"))
        {
            var currentUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);
            var tournament = await _tournamentService.GetByIdAsync(id);
            if (tournament == null) return NotFound();
            // In the real system, TournamentDto should probably have CreatorUserId too or we check in service.
            // For now, let's assume we implement the check in service or here.
            // I'll add ownership check to the service later or do it here.
            // Better do it in service.
        }

        var result = await _tournamentService.UpdateAsync(id, request);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _tournamentService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/close-registration")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<TournamentDto>> CloseRegistration(Guid id)
    {
        var result = await _tournamentService.CloseRegistrationAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/register")]
    public async Task<ActionResult<TeamRegistrationDto>> RegisterTeam(Guid id, RegisterTeamRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value!);
        var user = await _userService.GetByIdAsync(userId);
        if (user?.Status != "Active")
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من التسجيل في البطولات.");
        }
        
        var registration = await _tournamentService.RegisterTeamAsync(id, request, userId);
        return Ok(registration);
    }

    [HttpGet("{id}/registrations")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<IEnumerable<TeamRegistrationDto>>> GetRegistrations(Guid id)
    {
        var registrations = await _tournamentService.GetRegistrationsAsync(id);
        return Ok(registrations);
    }

    [HttpPost("{id}/registrations/{teamId}/payment")]
    public async Task<ActionResult<TeamRegistrationDto>> SubmitPayment(Guid id, Guid teamId, [FromForm] IFormFile receipt, [FromForm] string? senderNumber, [FromForm] string? paymentMethod)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
        
        var userId = Guid.Parse(userIdString);
        var user = await _userService.GetByIdAsync(userId);
        if (user?.Status != "Active")
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من إرسال إيصال الدفع.");
        }

        if (receipt == null || receipt.Length == 0) return BadRequest("يجب إرسال إيصال الدفع.");

        var receiptUrl = await SaveFile(receipt);
        var request = new SubmitPaymentRequest 
        { 
            PaymentReceiptUrl = receiptUrl,
            SenderNumber = senderNumber,
            PaymentMethod = paymentMethod
        };

        var registration = await _tournamentService.SubmitPaymentAsync(id, teamId, request, userId);
        return Ok(registration);
    }

    private async Task<string> SaveFile(IFormFile file)
    {
        // Convert to base64 data URL for hosting platforms that don't allow file writes
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();
        var base64 = Convert.ToBase64String(bytes);
        var mimeType = file.ContentType ?? "image/png";
        return $"data:{mimeType};base64,{base64}";
    }

    [HttpPost("{id}/registrations/{teamId}/approve")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<TeamRegistrationDto>> ApproveRegistration(Guid id, Guid teamId)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var userRole = User.IsInRole("Admin") ? "Admin" : "TournamentCreator";

        var result = await _tournamentService.ApproveRegistrationAsync(id, teamId, userId, userRole);
        return Ok(result);
    }

    [HttpPost("{id}/registrations/{teamId}/reject")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<TeamRegistrationDto>> RejectRegistration(Guid id, Guid teamId, RejectRegistrationRequest request)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var userRole = User.IsInRole("Admin") ? "Admin" : "TournamentCreator";

        var result = await _tournamentService.RejectRegistrationAsync(id, teamId, request, userId, userRole);
        return Ok(result);
    }

    [HttpGet("payments/pending")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<IEnumerable<PendingPaymentResponse>>> GetPendingPayments()
    {
        Guid? creatorId = null;
        if (User.IsInRole("TournamentCreator"))
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdStr))
            {
                creatorId = Guid.Parse(userIdStr);
            }
        }

        var pending = await _tournamentService.GetPendingPaymentsAsync(creatorId);
        return Ok(pending);
    }

    [HttpGet("payments/all")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<IEnumerable<PendingPaymentResponse>>> GetAllPaymentRequests()
    {
        Guid? creatorId = null;
        if (User.IsInRole("TournamentCreator"))
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdStr))
            {
                creatorId = Guid.Parse(userIdStr);
            }
        }

        var requests = await _tournamentService.GetAllPaymentRequestsAsync(creatorId);
        return Ok(requests);
    }

    [HttpPost("{id}/matches/generate")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GenerateMatches(Guid id)
    {
        var matches = await _tournamentService.GenerateMatchesAsync(id);
        return Ok(matches);
    }

    [HttpGet("{id}/standings")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TournamentStandingDto>>> GetStandings(Guid id, [FromQuery] int? groupId)
    {
        var standings = await _tournamentService.GetStandingsAsync(id, groupId);
        return Ok(standings);
    }

    [HttpGet("{id}/groups")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetGroups(Guid id)
    {
        var groups = await _tournamentService.GetGroupsAsync(id);
        return Ok(groups);
    }

    [HttpGet("{id}/bracket")]
    [AllowAnonymous]
    public async Task<ActionResult<BracketDto>> GetBracket(Guid id)
    {
        var bracket = await _tournamentService.GetBracketAsync(id);
        return Ok(bracket);
    }

    [HttpPost("{id}/eliminate/{teamId}")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<IActionResult> EliminateTeam(Guid id, Guid teamId)
    {
        await _tournamentService.EliminateTeamAsync(id, teamId);
        return NoContent();
    }

    [HttpPost("{id}/emergency/start")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<TournamentDto>> EmergencyStart(Guid id)
    {
        var result = await _tournamentService.EmergencyStartAsync(id);
        return Ok(result);
    }

    [HttpPost("{id}/emergency/end")]
    [Authorize(Roles = "Admin,TournamentCreator")]
    public async Task<ActionResult<TournamentDto>> EmergencyEnd(Guid id)
    {
        var result = await _tournamentService.EmergencyEndAsync(id);
        return Ok(result);
    }
}
