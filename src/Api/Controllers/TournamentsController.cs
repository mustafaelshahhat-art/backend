using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Application.Interfaces;
using Domain.Enums;
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
    private readonly MediatR.IMediator _mediator;

    public TournamentsController(ITournamentService tournamentService, IUserService userService, MediatR.IMediator mediator)
    {
        _tournamentService = tournamentService;
        _userService = userService;
        _mediator = mediator;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TournamentDto>>> GetAll()
    {
        Guid? creatorId = null;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        
        if (User.Identity?.IsAuthenticated == true && role == UserRole.TournamentCreator.ToString())
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

    [HttpGet("paged")]
    [AllowAnonymous]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TournamentDto>>> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        Guid? creatorId = null;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        
        if (User.Identity?.IsAuthenticated == true && role == UserRole.TournamentCreator.ToString())
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdStr))
            {
                creatorId = Guid.Parse(userIdStr);
            }
        }

        var result = await _tournamentService.GetPagedAsync(page, pageSize, creatorId);
        return Ok(result);
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
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<TournamentDto>> Create(CreateTournamentRequest request)
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        Guid? creatorId = null;
        
        if (role == UserRole.TournamentCreator.ToString())
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
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> Update(Guid id, UpdateTournamentRequest request)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.UpdateAsync(id, request, userId, userRole);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var (userId, userRole) = GetUserContext();
        await _tournamentService.DeleteAsync(id, userId, userRole);
        return NoContent();
    }

    [HttpPost("{id}/close-registration")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> CloseRegistration(Guid id)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.CloseRegistrationAsync(id, userId, userRole);
        return Ok(result);
    }

    [HttpPost("{id}/register")]
    public async Task<ActionResult<TeamRegistrationDto>> RegisterTeam(Guid id, RegisterTeamRequest request)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        var user = await _userService.GetByIdAsync(userId);
        if (user?.Status != UserStatus.Active.ToString())
        {
            return BadRequest("يجب تفعيل حسابك أولاً لتتمكن من التسجيل في البطولات.");
        }
        
        // PROD-AUDIT: Use Command for Transaction Safety
        var command = new Application.Features.Tournaments.Commands.RegisterTeam.RegisterTeamCommand(id, request.TeamId, userId);
        var registration = await _mediator.Send(command);
        return Ok(registration);
    }

    [HttpGet("{id}/registrations")]
    [Authorize(Policy = "RequireTournamentOwner")]
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
        if (user?.Status != UserStatus.Active.ToString())
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
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var bytes = memoryStream.ToArray();
        var base64 = Convert.ToBase64String(bytes);
        var mimeType = file.ContentType ?? "image/png";
        return $"data:{mimeType};base64,{base64}";
    }

    [HttpPost("{id}/registrations/{teamId}/approve")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TeamRegistrationDto>> ApproveRegistration(Guid id, Guid teamId)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.ApproveRegistrationAsync(id, teamId, userId, userRole);
        return Ok(result);
    }

    [HttpPost("{id}/registrations/{teamId}/reject")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TeamRegistrationDto>> RejectRegistration(Guid id, Guid teamId, RejectRegistrationRequest request)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.RejectRegistrationAsync(id, teamId, request, userId, userRole);
        return Ok(result);
    }

    [HttpPost("{id}/registrations/{teamId}/withdraw")]
    public async Task<IActionResult> WithdrawTeam(Guid id, Guid teamId)
    {
        var (userId, _) = GetUserContext();
        await _tournamentService.WithdrawTeamAsync(id, teamId, userId);
        return NoContent();
    }

    [HttpPost("{id}/registrations/{teamId}/promote")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TeamRegistrationDto>> PromoteWaitingTeam(Guid id, Guid teamId)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.PromoteWaitingTeamAsync(id, teamId, userId, userRole);
        return Ok(result);
    }

    [HttpGet("payments/pending")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<IEnumerable<PendingPaymentResponse>>> GetPendingPayments()
    {
        var (userId, userRole) = GetUserContext();
        Guid? creatorId = (userRole == UserRole.TournamentCreator.ToString()) ? userId : null;

        var pending = await _tournamentService.GetPendingPaymentsAsync(creatorId);
        return Ok(pending);
    }

    [HttpGet("payments/all")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<IEnumerable<PendingPaymentResponse>>> GetAllPaymentRequests()
    {
        var (userId, userRole) = GetUserContext();
        Guid? creatorId = (userRole == UserRole.TournamentCreator.ToString()) ? userId : null;

        var requests = await _tournamentService.GetAllPaymentRequestsAsync(creatorId);
        return Ok(requests);
    }

    [HttpPost("{id}/matches/generate")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GenerateMatches(Guid id)
    {
        var (userId, userRole) = GetUserContext();
        var matches = await _tournamentService.GenerateMatchesAsync(id, userId, userRole);
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
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> EliminateTeam(Guid id, Guid teamId)
    {
        var (userId, userRole) = GetUserContext();
        await _tournamentService.EliminateTeamAsync(id, teamId, userId, userRole);
        return NoContent();
    }

    [HttpPost("{id}/emergency/start")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> EmergencyStart(Guid id)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.EmergencyStartAsync(id, userId, userRole);
        return Ok(result);
    }

    [HttpPost("{id}/emergency/end")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> EmergencyEnd(Guid id)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.EmergencyEndAsync(id, userId, userRole);
        return Ok(result);
    }

    [HttpPost("{id}/opening-match")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> SetOpeningMatch(Guid id, [FromBody] OpeningMatchRequest request)
    {
        var (userId, userRole) = GetUserContext();
        var matches = await _tournamentService.SetOpeningMatchAsync(id, request.HomeTeamId, request.AwayTeamId, userId, userRole);
        return Ok(matches);
    }

    [HttpPost("{id}/manual-draw")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> ManualDraw(Guid id, ManualDrawRequest request)
    {
        var (userId, userRole) = GetUserContext();
        var matches = await _tournamentService.GenerateManualMatchesAsync(id, request, userId, userRole);
        return Ok(matches);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }
}
