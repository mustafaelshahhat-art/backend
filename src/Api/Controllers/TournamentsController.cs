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
        var (userId, userRole) = GetUserContextSafely();
        
        Guid? creatorIdFilter = null;
        if (userRole == UserRole.TournamentCreator.ToString())
        {
            creatorIdFilter = userId;
        }

        // PROD-FIX: Visibility Filtering logic. 
        // If not Owner/Creator, we only see RegistrationOpen, Active, Completed, etc. (everything except Draft).
        // The service method GetPagedAsync needs to handle this.
        var result = await _tournamentService.GetPagedAsync(page, pageSize, creatorIdFilter, cancellationToken);
        
        // Secondary safety filter: If it's public (Anonymous or Player), filter out Drafts 
        // which might have been returned if creatorIdFilter was null.
        if (userRole != UserRole.TournamentCreator.ToString() && userRole != UserRole.Admin.ToString())
        {
            result.Items = result.Items.Where(t => t.Status != TournamentStatus.Draft.ToString()).ToList();
        }

        return Ok(result);
    }

    [HttpGet("paged")]
    [AllowAnonymous]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TournamentDto>>> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        return await GetAll(page, pageSize, cancellationToken);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContextSafely();
        var tournament = await _tournamentService.GetByIdAsync(id, userId, cancellationToken);
        if (tournament == null) return NotFound();

        // PROD-FIX: Protection for Draft tournaments (redundant extra check)
        if (tournament.Status == TournamentStatus.Draft.ToString())
        {
            if (userRole != UserRole.Admin.ToString() && tournament.CreatorUserId != userId)
            {
                return Unauthorized();
            }
        }

        return Ok(tournament);
    }

    [HttpGet("active/team/{teamId}")]
    [AllowAnonymous]
    public async Task<ActionResult<TournamentDto>> GetActiveByTeam(Guid teamId, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentService.GetActiveByTeamAsync(teamId, cancellationToken);
        if (tournament == null) return NotFound();
        // Drafts are never "active" by definition in service logic
        return Ok(tournament);
    }

    [HttpGet("{id}/registration/team/{teamId}")]
    [AllowAnonymous]
    public async Task<ActionResult<TeamRegistrationDto>> GetTeamRegistration(Guid id, Guid teamId, CancellationToken cancellationToken)
    {
        var registration = await _tournamentService.GetRegistrationByTeamAsync(id, teamId, cancellationToken);
        if (registration == null) return NotFound();
        return Ok(registration);
    }

    [HttpPost]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<TournamentDto>> Create(CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContextSafely();
        

        var command = new Application.Features.Tournaments.Commands.CreateTournament.CreateTournamentCommand(request, userId);
        var result = await _mediator.Send(command, cancellationToken);


        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    private (Guid userId, string userRole) GetUserContextSafely()
    {
        try 
        {
            var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "/";
            if (string.IsNullOrEmpty(idStr)) return (Guid.Empty, "/");
            return (Guid.Parse(idStr), role);
        }
        catch { return (Guid.Empty, "/"); }
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> Update(Guid id, UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.UpdateTournament.UpdateTournamentCommand(id, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.DeleteTournament.DeleteTournamentCommand(id, userId, userRole);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/close-registration")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> CloseRegistration(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.CloseRegistration.CloseRegistrationCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
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
    public async Task<ActionResult<Application.Common.Models.PagedResult<TeamRegistrationDto>>> GetRegistrations(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var registrations = await _tournamentService.GetRegistrationsAsync(id, page, pageSize, cancellationToken);
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
        var command = new Application.Features.Tournaments.Commands.RejectRegistration.RejectRegistrationCommand(id, teamId, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
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
        var command = new Application.Features.Tournaments.Commands.PromoteWaitingTeam.PromoteWaitingTeamCommand(id, teamId, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("payments/pending")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<PendingPaymentResponse>>> GetPendingPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var (userId, userRole) = GetUserContext();
        Guid? creatorId = (userRole == UserRole.TournamentCreator.ToString()) ? userId : null;

        var pending = await _tournamentService.GetPendingPaymentsAsync(page, pageSize, creatorId, cancellationToken);
        return Ok(pending);
    }

    [HttpGet("payments/all")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<PendingPaymentResponse>>> GetAllPaymentRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var (userId, userRole) = GetUserContext();
        Guid? creatorId = (userRole == UserRole.TournamentCreator.ToString()) ? userId : null;

        var requests = await _tournamentService.GetAllPaymentRequestsAsync(page, pageSize, creatorId, cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{id}/generate-matches")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GenerateMatches(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.GenerateMatches.GenerateMatchesCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}/standings")]
    [AllowAnonymous]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TournamentStandingDto>>> GetStandings(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] int? groupId = null, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var standings = await _tournamentService.GetStandingsAsync(id, page, pageSize, groupId, cancellationToken);
        return Ok(standings);
    }

    [HttpGet("{id}/groups")]
    [AllowAnonymous]
    public async Task<ActionResult<Application.Common.Models.PagedResult<GroupDto>>> GetGroups(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var groups = await _tournamentService.GetGroupsAsync(id, page, pageSize, cancellationToken);
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
    public async Task<ActionResult> EliminateTeam(Guid id, Guid teamId, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.EliminateTeam.EliminateTeamCommand(id, teamId, userId, userRole);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/emergency-start")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> EmergencyStart(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.EmergencyStart.EmergencyStartCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/emergency-end")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> EmergencyEnd(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.EmergencyEnd.EmergencyEndCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/opening-match")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> SetOpeningMatch(Guid id, [FromBody] OpeningMatchRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.SetOpeningMatch.SetOpeningMatchCommand(id, request.HomeTeamId, request.AwayTeamId, userId, userRole);
        var matches = await _mediator.Send(command, cancellationToken);
        return Ok(matches);
    }

    [HttpPost("{id}/manual-draw")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> ManualDraw(Guid id, [FromBody] ManualDrawRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.ManualDraw.ManualDrawCommand(id, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    private (Guid userId, string userRole) GetUserContext()
    {
        var idStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        return (Guid.Parse(idStr!), role);
    }
}
