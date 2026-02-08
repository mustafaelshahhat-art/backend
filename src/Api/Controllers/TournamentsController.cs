using Application.DTOs.Tournaments;
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
        var tournaments = await _tournamentService.GetAllAsync();
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TournamentDto>> Create(CreateTournamentRequest request)
    {
        var tournament = await _tournamentService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, tournament);
    }

    [HttpPatch("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TournamentDto>> Update(Guid id, UpdateTournamentRequest request)
    {
        var tournament = await _tournamentService.UpdateAsync(id, request);
        return Ok(tournament);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _tournamentService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/close-registration")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TournamentDto>> CloseRegistration(Guid id)
    {
        var tournament = await _tournamentService.CloseRegistrationAsync(id);
        return Ok(tournament);
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<TeamRegistrationDto>>> GetRegistrations(Guid id)
    {
        var registrations = await _tournamentService.GetRegistrationsAsync(id);
        return Ok(registrations);
    }

    [HttpPost("{id}/registrations/{teamId}/payment")]
    public async Task<ActionResult<TeamRegistrationDto>> SubmitPayment(Guid id, Guid teamId, [FromForm] IFormFile receipt, [FromForm] string? senderNumber)
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
            SenderNumber = senderNumber 
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
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeamRegistrationDto>> ApproveRegistration(Guid id, Guid teamId)
    {
        var registration = await _tournamentService.ApproveRegistrationAsync(id, teamId);
        return Ok(registration);
    }

    [HttpPost("{id}/registrations/{teamId}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TeamRegistrationDto>> RejectRegistration(Guid id, Guid teamId, RejectRegistrationRequest request)
    {
        var registration = await _tournamentService.RejectRegistrationAsync(id, teamId, request);
        return Ok(registration);
    }

    [HttpGet("payments/pending")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<PendingPaymentResponse>>> GetPendingPayments()
    {
        var pending = await _tournamentService.GetPendingPaymentsAsync();
        return Ok(pending);
    }

    [HttpGet("payments/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<PendingPaymentResponse>>> GetAllPaymentRequests()
    {
        var requests = await _tournamentService.GetAllPaymentRequestsAsync();
        return Ok(requests);
    }

    [HttpPost("{id}/generate-matches")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> GenerateMatches(Guid id)
    {
        var matches = await _tournamentService.GenerateMatchesAsync(id);
        return Ok(new { message = $"تم توليد {matches.Count()} مباراة بنجاح", matches });
    }

    [HttpGet("{id}/standings")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TournamentStandingDto>>> GetStandings(Guid id)
    {
        var standings = await _tournamentService.GetStandingsAsync(id);
        return Ok(standings);
    }

    [HttpPost("{id}/registrations/{teamId}/eliminate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> EliminateTeam(Guid id, Guid teamId)
    {
        await _tournamentService.EliminateTeamAsync(id, teamId);
        return NoContent();
    }
}
