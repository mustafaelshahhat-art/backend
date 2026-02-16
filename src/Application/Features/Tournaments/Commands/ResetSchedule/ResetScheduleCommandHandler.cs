using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.ResetSchedule;

public class ResetScheduleCommandHandler : IRequestHandler<ResetScheduleCommand, Unit>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IDistributedLock _distributedLock;

    public ResetScheduleCommandHandler(
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

    public async Task<Unit> Handle(ResetScheduleCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("عملية جدولة أخرى قيد التنفيذ لهذا الدوري.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
            if (tournament == null) throw new NotFoundException("البطولة غير موجودة.");

            if (request.UserRole != UserRole.Admin.ToString() && tournament.CreatorUserId != request.UserId)
            {
                throw new ForbiddenException("ليس لديك صلاحية لتعديل جدولة هذه البطولة.");
            }

            if (tournament.Status == TournamentStatus.Active || tournament.Status == TournamentStatus.Completed)
            {
                throw new BadRequestException("لا يمكن إعادة تعيين الجدولة لبطولة بدأت بالفعل.");
            }

            // Delete all matches
            var matches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            await _matchRepository.DeleteRangeAsync(matches, cancellationToken);

            // SECTION 7: Clear opening team selection on reset
            tournament.ClearOpeningTeams();
            tournament.OpeningMatchId = null;
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            // Clear GroupIds from registrations — batch update (single SaveChanges)
            var registrations = (await _registrationRepository.FindAsync(
                r => r.TournamentId == request.TournamentId, cancellationToken)).ToList();
            foreach (var reg in registrations)
            {
                reg.GroupId = null;
            }
            if (registrations.Any())
            {
                await _registrationRepository.UpdateRangeAsync(registrations, cancellationToken);
            }

            return Unit.Value;
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
