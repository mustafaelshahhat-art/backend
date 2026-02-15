using Application.Interfaces;
using Application.Features.Tournaments.Commands.GenerateMatches;
using Application.DTOs.Matches;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.SetOpeningMatch;

/// <summary>
/// PRE-DRAW: Sets two teams as the opening match BEFORE schedule generation.
/// - Only Creator/Admin.
/// - Only if matches not generated.
/// - Must run before GenerateScheduleCommand.
/// - Uses DistributedLock.
/// - Overrides previous opening selection if exists.
/// - Cannot be called after schedule generated.
/// - AUTOMATION: Automatically triggers match generation for Random scheduling mode.
/// </summary>
public class SetOpeningMatchCommandHandler : IRequestHandler<SetOpeningMatchCommand, IEnumerable<MatchDto>>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IDistributedLock _distributedLock;
    private readonly IMediator _mediator;
    private readonly ITournamentService _tournamentService;

    public SetOpeningMatchCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Match> matchRepository,
        IDistributedLock distributedLock,
        IMediator mediator,
        ITournamentService tournamentService)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _distributedLock = distributedLock;
        _mediator = mediator;
        _tournamentService = tournamentService;
    }

    public async Task<IEnumerable<MatchDto>> Handle(SetOpeningMatchCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("العملية قيد التنفيذ من قبل مستخدم آخر.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

            // Authorization: Only Creator or Admin
            var isAdmin = request.UserRole == UserRole.Admin.ToString();
            var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
            if (!isAdmin && !isOwner)
                throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

            // Validate: No matches already generated
            var matchesExist = await _matchRepository.AnyAsync(m => m.TournamentId == request.TournamentId, cancellationToken);

            // STRICT: Validate all teams are approved (Payment Lock System)
            var allActiveRegistrations = await _registrationRepository.FindAsync(
                r => r.TournamentId == request.TournamentId && 
                     r.Status != RegistrationStatus.Rejected && 
                     r.Status != RegistrationStatus.Withdrawn &&
                     r.Status != RegistrationStatus.WaitingList, 
                cancellationToken);

            if (allActiveRegistrations.Any(r => r.Status != RegistrationStatus.Approved))
            {
                throw new ConflictException("بانتظار اكتمال الموافقة على جميع المدفوعات.");
            }

            // Ensure we have enough teams
            if (allActiveRegistrations.Count() < (tournament.MinTeams ?? 2))
            {
                 throw new ConflictException("عدد الفرق غير كاف لبدء البطولة.");
            }

            var registeredTeamIds = allActiveRegistrations.Select(r => r.TeamId);

            // Domain validation via entity method (encapsulated)
            tournament.SetOpeningTeams(request.HomeTeamId, request.AwayTeamId, registeredTeamIds, matchesExist);

            // Persist within same transaction
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            // AUTOMATION: If tournament is in Random scheduling mode, automatically generate matches
            IEnumerable<MatchDto> generatedMatches = new List<MatchDto>();
            if (tournament.SchedulingMode == SchedulingMode.Random)
            {
                // Call Service DIRECTLY to avoid Distributed Lock Deadlock (since we already hold the lock)
                generatedMatches = await _tournamentService.GenerateMatchesAsync(request.TournamentId, request.UserId, request.UserRole, cancellationToken);
            }

            return generatedMatches;
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
