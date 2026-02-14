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
    public async Task<ActionResult<Application.Common.Models.PagedResult<TournamentDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
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
 
        var result = await _tournamentService.GetPagedAsync(page, pageSize, creatorId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("paged")]
    [AllowAnonymous]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TournamentDto>>> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
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

        var result = await _tournamentService.GetPagedAsync(page, pageSize, creatorId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentService.GetByIdAsync(id, cancellationToken);
        if (tournament == null) return NotFound();
        return Ok(tournament);
    }

    [HttpPost]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<TournamentDto>> Create(CreateTournamentRequest request, CancellationToken cancellationToken)
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
        
        var tournament = await _tournamentService.CreateAsync(request, creatorId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, tournament);
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> Update(Guid id, UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.UpdateAsync(id, request, userId, userRole, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        await _tournamentService.DeleteAsync(id, userId, userRole, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/close-registration")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> CloseRegistration(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.CloseRegistrationAsync(id, userId, userRole, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/register")]
    public async Task<ActionResult<TeamRegistrationDto>> RegisterTeam(Guid id, RegisterTeamRequest request, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdStr)) return Unauthorized();

        var userId = Guid.Parse(userIdStr);
        
        // PROD-AUDIT: Use Command for Transaction Safety (Validation now handled by Pipeline)
        var command = new Application.Features.Tournaments.Commands.RegisterTeam.RegisterTeamCommand(id, request.TeamId, userId);
        var registration = await _mediator.Send(command, cancellationToken);
        return Ok(registration);
    }

    [HttpGet("{id}/registrations")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<TeamRegistrationDto>>> GetRegistrations(Guid id, CancellationToken cancellationToken)
    {
        var registrations = await _tournamentService.GetRegistrationsAsync(id, cancellationToken);
        return Ok(registrations);
    }

    [HttpPost("{id}/registrations/{teamId}/payment")]
    [Api.Infrastructure.Filters.FileValidation]
    public async Task<ActionResult<TeamRegistrationDto>> SubmitPayment(Guid id, Guid teamId, [FromForm] IFormFile receipt, [FromForm] string? senderNumber, [FromForm] string? paymentMethod, CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
        
        var userId = Guid.Parse(userIdString);

        var receiptUrl = await SaveFile(receipt, cancellationToken);
        var command = new Application.Features.Tournaments.Commands.SubmitPayment.SubmitPaymentCommand(
            id, 
            teamId, 
            userId, 
            receiptUrl, 
            senderNumber, 
            paymentMethod);

        var registration = await _mediator.Send(command, cancellationToken);
        return Ok(registration);
    }

    private async Task<string> SaveFile(IFormFile file, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();
        var base64 = Convert.ToBase64String(bytes);
        var mimeType = file.ContentType ?? "image/png";
        return $"data:{mimeType};base64,{base64}";
    }

    [HttpPost("{id}/registrations/{teamId}/approve")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TeamRegistrationDto>> ApproveRegistration(Guid id, Guid teamId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.ApproveRegistration.ApproveRegistrationCommand(id, teamId, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/registrations/{teamId}/reject")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TeamRegistrationDto>> RejectRegistration(Guid id, Guid teamId, RejectRegistrationRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.RejectRegistrationAsync(id, teamId, request, userId, userRole, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/registrations/{teamId}/withdraw")]
    public async Task<IActionResult> WithdrawTeam(Guid id, Guid teamId, CancellationToken cancellationToken)
    {
        var (userId, _) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.WithdrawTeam.WithdrawTeamCommand(id, teamId, userId);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/registrations/{teamId}/promote")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TeamRegistrationDto>> PromoteWaitingTeam(Guid id, Guid teamId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.PromoteWaitingTeamAsync(id, teamId, userId, userRole, cancellationToken);
        return Ok(result);
    }

    [HttpGet("payments/pending")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<IEnumerable<PendingPaymentResponse>>> GetPendingPayments(CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        Guid? creatorId = (userRole == UserRole.TournamentCreator.ToString()) ? userId : null;

        var pending = await _tournamentService.GetPendingPaymentsAsync(creatorId, cancellationToken);
        return Ok(pending);
    }

    [HttpGet("payments/all")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<IEnumerable<PendingPaymentResponse>>> GetAllPaymentRequests(CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        Guid? creatorId = (userRole == UserRole.TournamentCreator.ToString()) ? userId : null;

        var requests = await _tournamentService.GetAllPaymentRequestsAsync(creatorId, cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{id}/matches/generate")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GenerateMatches(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var matches = await _tournamentService.GenerateMatchesAsync(id, userId, userRole, cancellationToken);
        return Ok(matches);
    }

    [HttpGet("{id}/standings")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TournamentStandingDto>>> GetStandings(Guid id, [FromQuery] int? groupId, CancellationToken cancellationToken)
    {
        var standings = await _tournamentService.GetStandingsAsync(id, groupId, cancellationToken);
        return Ok(standings);
    }

    [HttpGet("{id}/groups")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<GroupDto>>> GetGroups(Guid id, CancellationToken cancellationToken)
    {
        var groups = await _tournamentService.GetGroupsAsync(id, cancellationToken);
        return Ok(groups);
    }

    [HttpGet("{id}/bracket")]
    [AllowAnonymous]
    public async Task<ActionResult<BracketDto>> GetBracket(Guid id, CancellationToken cancellationToken)
    {
        var bracket = await _tournamentService.GetBracketAsync(id, cancellationToken);
        return Ok(bracket);
    }

    [HttpPost("{id}/eliminate/{teamId}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> EliminateTeam(Guid id, Guid teamId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        await _tournamentService.EliminateTeamAsync(id, teamId, userId, userRole, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/emergency/start")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<TournamentDto>> EmergencyStart(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.EmergencyStartAsync(id, userId, userRole, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/emergency/end")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<ActionResult<TournamentDto>> EmergencyEnd(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var result = await _tournamentService.EmergencyEndAsync(id, userId, userRole, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/opening-match")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> SetOpeningMatch(Guid id, [FromBody] OpeningMatchRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var matches = await _tournamentService.SetOpeningMatchAsync(id, request.HomeTeamId, request.AwayTeamId, userId, userRole, cancellationToken);
        return Ok(matches);
    }

    [HttpPost("{id}/manual-draw")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> ManualDraw(Guid id, ManualDrawRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var matches = await _tournamentService.GenerateManualMatchesAsync(id, request, userId, userRole, cancellationToken);
        return Ok(matches);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }
}
