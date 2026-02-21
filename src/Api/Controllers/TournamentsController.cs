using Application.DTOs;
using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TournamentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IOutputCacheStore _cacheStore;

    public TournamentsController(IMediator mediator, IOutputCacheStore cacheStore)
    {
        _mediator = mediator;
        _cacheStore = cacheStore;
    }

    [HttpGet]
    [AllowAnonymous]
    [OutputCache(PolicyName = "TournamentList")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TournamentDto>>> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var (userId, userRole) = GetUserContext();

        var query = new Application.Features.Tournaments.Queries.GetTournamentsPaged.GetTournamentsPagedQuery(page, pageSize, userId, userRole);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("paged")]
    [AllowAnonymous]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TournamentDto>>> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        return await GetAll(page, pageSize, cancellationToken);
    }

    [HttpGet("{id}/matches")]
    [AllowAnonymous]
    [OutputCache(PolicyName = "MatchList")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<MatchDto>>> GetMatches(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        // PERF: Cap reduced from 500 → 100. A 500-row JSON payload on a 256MB host
        // materializes ~2-4MB per request in memory before compression.
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Matches.Queries.GetMatchesByTournament.GetMatchesByTournamentQuery(id, page, pageSize);
        var matches = await _mediator.Send(query, cancellationToken);
        return Ok(matches);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    [OutputCache(PolicyName = "TournamentDetail")]
    public async Task<ActionResult<TournamentDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var query = new Application.Tournaments.Queries.GetTournamentById.GetTournamentByIdQuery(id, userId, userRole);
        var tournament = await _mediator.Send(query, cancellationToken);
        if (tournament == null) return NotFound();

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
        var query = new Application.Features.Tournaments.Queries.GetActiveTournamentByTeam.GetActiveTournamentByTeamQuery(teamId);
        var tournament = await _mediator.Send(query, cancellationToken);
        if (tournament == null) return NotFound();
        return Ok(tournament);
    }

    [HttpGet("{id}/registration/team/{teamId}")]
    [AllowAnonymous]
    public async Task<ActionResult<TeamRegistrationDto>> GetTeamRegistration(Guid id, Guid teamId, CancellationToken cancellationToken)
    {
        var query = new Application.Features.Tournaments.Queries.GetRegistrationByTeam.GetRegistrationByTeamQuery(id, teamId);
        var registration = await _mediator.Send(query, cancellationToken);
        if (registration == null) return NotFound();
        return Ok(registration);
    }

    [HttpPost]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<TournamentDto>> Create(CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        var (userId, _) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.CreateTournament.CreateTournamentCommand(request, userId);
        var result = await _mediator.Send(command, cancellationToken);
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> Update(Guid id, UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.UpdateTournament.UpdateTournamentCommand(id, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.DeleteTournament.DeleteTournamentCommand(id, userId, userRole);
        await _mediator.Send(command, cancellationToken);
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
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

    [HttpPost("{id}/start")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<TournamentDto>> StartTournament(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.StartTournament.StartTournamentCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
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
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
        return StatusCode(StatusCodes.Status201Created, registration);
    }

    [HttpGet("{id}/registrations")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TeamRegistrationDto>>> GetRegistrations(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Tournaments.Queries.GetRegistrations.GetRegistrationsQuery(id, page, pageSize);
        var registrations = await _mediator.Send(query, cancellationToken);
        return Ok(registrations);
    }

    [HttpPost("{id}/registrations/{teamId}/payment")]
    [Api.Infrastructure.Filters.FileValidation]
    public async Task<ActionResult<TeamRegistrationDto>> SubmitPayment(Guid id, Guid teamId, [FromForm] IFormFile receipt, [FromForm] string? senderNumber, [FromForm] string? paymentMethod, CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdString)) return Unauthorized();
        
        var userId = Guid.Parse(userIdString);

        using var stream = receipt.OpenReadStream();

        var command = new Application.Features.Tournaments.Commands.SubmitPayment.SubmitPaymentCommand(
            id, 
            teamId, 
            userId, 
            stream, 
            receipt.FileName, 
            receipt.ContentType, 
            senderNumber, 
            paymentMethod);

        var registration = await _mediator.Send(command, cancellationToken);
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
        return Ok(registration);
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
    public async Task<ActionResult<Application.Common.Models.PagedResult<PendingPaymentResponse>>> GetPendingPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var (userId, userRole) = GetUserContext();
        Guid? creatorId = (userRole == UserRole.TournamentCreator.ToString()) ? userId : null;

        var query = new Application.Features.Tournaments.Queries.GetPendingPayments.GetPendingPaymentsQuery(page, pageSize, creatorId);
        var pending = await _mediator.Send(query, cancellationToken);
        return Ok(pending);
    }

    [HttpGet("payments/all")]
    [Authorize(Policy = "RequireCreator")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<PendingPaymentResponse>>> GetAllPaymentRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var (userId, userRole) = GetUserContext();
        Guid? creatorId = (userRole == UserRole.TournamentCreator.ToString()) ? userId : null;

        var query = new Application.Features.Tournaments.Queries.GetAllPaymentRequests.GetAllPaymentRequestsQuery(page, pageSize, creatorId);
        var requests = await _mediator.Send(query, cancellationToken);
        return Ok(requests);
    }

    [HttpPost("{id}/generate-matches")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<MatchListResponse>> GenerateMatches(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.GenerateMatches.GenerateMatchesCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        // Evict both: tournament status changes + new matches created
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
        await _cacheStore.EvictByTagAsync("matches", cancellationToken);
        await _cacheStore.EvictByTagAsync("standings", cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id}/standings")]
    [AllowAnonymous]
    [OutputCache(PolicyName = "Standings")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<TournamentStandingDto>>> GetStandings(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] int? groupId = null, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Tournaments.Queries.GetStandings.GetStandingsQuery(id, page, pageSize, groupId);
        var standings = await _mediator.Send(query, cancellationToken);
        return Ok(standings);
    }

    [HttpGet("{id}/groups")]
    [AllowAnonymous]
    [OutputCache(PolicyName = "ShortCache")]
    public async Task<ActionResult<Application.Common.Models.PagedResult<GroupDto>>> GetGroups(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (pageSize > 100) pageSize = 100;
        var query = new Application.Features.Tournaments.Queries.GetGroups.GetGroupsQuery(id, page, pageSize);
        var groups = await _mediator.Send(query, cancellationToken);
        return Ok(groups);
    }

    [HttpGet("{id}/bracket")]
    [AllowAnonymous]
    [OutputCache(PolicyName = "ShortCache")]
    public async Task<ActionResult<BracketDto>> GetBracket(Guid id, CancellationToken cancellationToken)
    {
        var query = new Application.Features.Tournaments.Queries.GetBracket.GetBracketQuery(id);
        var bracket = await _mediator.Send(query, cancellationToken);
        return Ok(bracket);
    }

    [HttpPost("{id}/eliminate/{teamId}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> EliminateTeam(Guid id, Guid teamId, CancellationToken cancellationToken)
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
    public async Task<ActionResult<MatchListResponse>> SetOpeningMatch(Guid id, [FromBody] OpeningMatchRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.SetOpeningMatch.SetOpeningMatchCommand(id, request.HomeTeamId, request.AwayTeamId, userId, userRole);
        var matches = await _mediator.Send(command, cancellationToken);
        // Evict output caches so the next GET /standings, /groups, and /tournaments/{id}
        // return fresh data with the newly generated matches and GroupId assignments.
        await _cacheStore.EvictByTagAsync("standings", cancellationToken);
        await _cacheStore.EvictByTagAsync("matches", cancellationToken);
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
        return Ok(matches);
    }

    [HttpPost("{id}/manual-draw")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<MatchListResponse>> ManualDraw(Guid id, [FromBody] ManualDrawRequest request, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.ManualDraw.ManualDrawCommand(id, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
        await _cacheStore.EvictByTagAsync("matches", cancellationToken);
        await _cacheStore.EvictByTagAsync("standings", cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/assign-groups")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> AssignGroups(Guid id, [FromBody] List<GroupAssignmentDto> assignments, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.AssignTeamsToGroups.AssignTeamsToGroupsCommand(id, assignments, userId, userRole);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/generate-manual-group-matches")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<MatchListResponse>> GenerateManualGroupMatches(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.GenerateManualGroupMatches.GenerateManualGroupMatchesCommand(id, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        await _cacheStore.EvictByTagAsync("tournaments", cancellationToken);
        await _cacheStore.EvictByTagAsync("matches", cancellationToken);
        await _cacheStore.EvictByTagAsync("standings", cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/manual-knockout-pairings")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<MatchListResponse>> CreateManualKnockoutMatches(Guid id, [FromBody] List<KnockoutPairingDto> pairings, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.CreateManualKnockoutMatches.CreateManualKnockoutMatchesCommand(id, pairings, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Submit organiser-supplied pairings for the next knockout round (Round 2, Semi-final).
    /// Only valid for Manual-mode tournaments. The Final is always auto-generated.
    /// Called after the lifecycle service signals ManualDrawRequired = true.
    /// </summary>
    [HttpPost("{id}/manual-next-round/{roundNumber:int}")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<MatchListResponse>> CreateManualNextRound(
        Guid id,
        int roundNumber,
        [FromBody] List<KnockoutPairingDto> pairings,
        CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.CreateManualNextRound.CreateManualNextRoundCommand(
            id, roundNumber, pairings, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Organiser confirms which teams advance from the group stage to the knockout round.
    /// Only valid for Manual-mode tournaments when status is ManualQualificationPending.
    /// Marks the selected teams, transitions the tournament to QualificationConfirmed,
    /// then immediately generates knockout round 1.
    /// </summary>
    [HttpPost("{id}/confirm-manual-qualification")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<ActionResult<Application.DTOs.Tournaments.TournamentLifecycleResult>> ConfirmManualQualification(
        Guid id,
        [FromBody] Application.DTOs.Tournaments.ConfirmManualQualificationRequest request,
        CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.ConfirmManualQualification.ConfirmManualQualificationCommand(
            id, request, userId, userRole);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id}/reset-schedule")]
    [Authorize(Policy = "RequireTournamentOwner")]
    public async Task<IActionResult> ResetSchedule(Guid id, CancellationToken cancellationToken)
    {
        var (userId, userRole) = GetUserContext();
        var command = new Application.Features.Tournaments.Commands.ResetSchedule.ResetScheduleCommand(id, userId, userRole);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id}/refresh-status")]
    [Authorize(Policy = "RequireTournamentOwner")] // Can be restricted, but useful for admins too
    public async Task<ActionResult<TournamentLifecycleResult>> RefreshStatus(Guid id, CancellationToken cancellationToken)
    {
        var command = new Application.Features.Tournaments.Commands.RefreshTournamentStatus.RefreshTournamentStatusCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    private (Guid UserId, string UserRole) GetUserContext()
    {
        var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var roleClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? UserRole.Player.ToString();
        
        return (Guid.TryParse(idClaim, out var userId) ? userId : Guid.Empty, roleClaim);
    }
}
