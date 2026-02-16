using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.AssignTeamsToGroups;

public class AssignTeamsToGroupsCommandHandler : IRequestHandler<AssignTeamsToGroupsCommand, Unit>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IDistributedLock _distributedLock;

    public AssignTeamsToGroupsCommandHandler(
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

    public async Task<Unit> Handle(AssignTeamsToGroupsCommand request, CancellationToken cancellationToken)
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

            // Ownership enforced
            if (request.UserRole != UserRole.Admin.ToString() && tournament.CreatorUserId != request.UserId)
            {
                throw new ForbiddenException("ليس لديك صلاحية لتعديل جدولة هذه البطولة.");
            }

            // SchedulingMode must be Manual
            if (tournament.SchedulingMode != SchedulingMode.Manual)
            {
                throw new BadRequestException("البطولة ليست في وضع الجدولة اليدوية.");
            }

            // Tournament must be RegistrationClosed
            if (tournament.Status != TournamentStatus.RegistrationClosed)
            {
                throw new BadRequestException("يجب إغلاق التسجيل قبل تعيين المجموعات.");
            }

            // Cannot assign if matches already generated
            var matchesExist = await _matchRepository.AnyAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (matchesExist)
            {
                throw new BadRequestException("لا يمكن تغيير تعيين المجموعات بعد إنشاء المباريات.");
            }

            var allRegistrations = await _registrationRepository.FindAsync(
                r => r.TournamentId == request.TournamentId && 
                     r.Status == RegistrationStatus.Approved, 
                cancellationToken);

            var registeredTeamIds = allRegistrations.Select(r => r.TeamId).ToHashSet();
            var assignedTeamIds = request.Assignments.SelectMany(a => a.TeamIds).ToList();

            // Validate consistency: Teams must belong to tournament
            if (!assignedTeamIds.All(id => registeredTeamIds.Contains(id)))
            {
                throw new BadRequestException("بعض الفرق المحددة غير مسجلة أو لم يتم الموافقة عليها في هذه البطولة.");
            }

            // No duplicate team across groups
            if (assignedTeamIds.Count != assignedTeamIds.Distinct().Count())
            {
                throw new BadRequestException("لا يمكن تكرار الفريق في أكثر من مجموعة.");
            }

            // All registered teams must be assigned exactly once
            if (assignedTeamIds.Count != registeredTeamIds.Count)
            {
                throw new BadRequestException("يجب تعيين جميع الفرق المسجلة في المجموعات.");
            }

            // Group capacity and count (basic validation)
            if (request.Assignments.Count != tournament.NumberOfGroups)
            {
                throw new BadRequestException($"يجب تعيين الفرق لعدد {tournament.NumberOfGroups} مجموعة بالضبط.");
            }

            // ============================================================
            // SECTION 4 — MANUAL MODE: Opening Teams MUST be in same group
            // ============================================================
            if (tournament.HasOpeningTeams)
            {
                var openingA = tournament.OpeningTeamAId!.Value;
                var openingB = tournament.OpeningTeamBId!.Value;

                var groupOfA = request.Assignments.FirstOrDefault(a => a.TeamIds.Contains(openingA))?.GroupId;
                var groupOfB = request.Assignments.FirstOrDefault(a => a.TeamIds.Contains(openingB))?.GroupId;

                if (groupOfA == null || groupOfB == null)
                {
                    throw new BadRequestException("فريقا المباراة الافتتاحية يجب أن يكونا ضمن المجموعات المُعيّنة.");
                }

                if (groupOfA != groupOfB)
                {
                    throw new BadRequestException("فريقا المباراة الافتتاحية يجب أن يكونا في نفس المجموعة. الرجاء تعديل التوزيع.");
                }
            }

            // Persistence — batch update (single SaveChanges round-trip)
            var registrationsToUpdate = new List<TeamRegistration>();
            foreach (var assignment in request.Assignments)
            {
                foreach (var teamId in assignment.TeamIds)
                {
                    var registration = allRegistrations.First(r => r.TeamId == teamId);
                    registration.GroupId = assignment.GroupId;
                    registrationsToUpdate.Add(registration);
                }
            }
            await _registrationRepository.UpdateRangeAsync(registrationsToUpdate, cancellationToken);

            return Unit.Value;
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
