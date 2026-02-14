using Application.Interfaces;
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
/// </summary>
public class SetOpeningMatchCommandHandler : IRequestHandler<SetOpeningMatchCommand, Unit>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IDistributedLock _distributedLock;

    public SetOpeningMatchCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Match> matchRepository,
        IDistributedLock distributedLock)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _distributedLock = distributedLock;
    }

    public async Task<Unit> Handle(SetOpeningMatchCommand request, CancellationToken cancellationToken)
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

            // Get registered (approved) team IDs
            var registrations = await _registrationRepository.FindAsync(
                r => r.TournamentId == request.TournamentId && r.Status == RegistrationStatus.Approved, 
                cancellationToken);
            var registeredTeamIds = registrations.Select(r => r.TeamId);

            // Domain validation via entity method (encapsulated)
            tournament.SetOpeningTeams(request.HomeTeamId, request.AwayTeamId, registeredTeamIds, matchesExist);

            // Persist within same transaction
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            return Unit.Value;
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
