using Application.DTOs.Tournaments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.ConfirmManualQualification;

/// <summary>
/// Validates organiser-supplied qualification selections, persists them,
/// then immediately triggers knockout-round-1 generation.
///
/// DESIGN NOTES
/// ────────────
/// • Enforces domain rule via Tournament.RequiresManualQualification().
/// • Delegates knockout seeding to ITournamentLifecycleService.GenerateKnockoutR1Async
///   which reads TeamRegistration.IsQualifiedForKnockout for the QualificationConfirmed path.
/// • &lt;= 5 constructor dependencies.
/// • Handler body &lt;= 100 lines.
/// </summary>
public class ConfirmManualQualificationCommandHandler
    : IRequestHandler<ConfirmManualQualificationCommand, TournamentLifecycleResult>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IDistributedLock _distributedLock;
    private readonly ITournamentLifecycleService _lifecycleService;

    public ConfirmManualQualificationCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IDistributedLock distributedLock,
        ITournamentLifecycleService lifecycleService)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _distributedLock = distributedLock;
        _lifecycleService = lifecycleService;
    }

    public async Task<TournamentLifecycleResult> Handle(
        ConfirmManualQualificationCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-matches-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(15), cancellationToken))
            throw new ConflictException("يتم معالجة مباريات هذه البطولة من قبل مستخدم آخر حالياً.");

        try
        {
            // ── Load & authorise ──────────────────────────────────────────────────
            var tournament = await _tournamentRepository.GetByIdAsync(
                request.TournamentId, cancellationToken);
            if (tournament is null)
                throw new NotFoundException(nameof(Tournament), request.TournamentId);

            var isAdmin = request.UserRole == UserRole.Admin.ToString();
            var isOwner = request.UserRole == UserRole.TournamentCreator.ToString()
                          && tournament.CreatorUserId == request.UserId;
            if (!isAdmin && !isOwner)
                throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

            // ── Domain guard ──────────────────────────────────────────────────────
            if (!tournament.RequiresManualQualification())
                throw new BadRequestException(
                    tournament.Status == TournamentStatus.QualificationConfirmed
                        ? "تم تأكيد التأهل بالفعل."
                        : "البطولة ليست في مرحلة انتظار التأهل اليدوي.");

            if (!request.Request.Selections.Any())
                throw new BadRequestException("يجب تحديد فرق التأهل لكل مجموعة.");

            // ── Load registrations ────────────────────────────────────────────────
            var allRegistrations = await _registrationRepository.FindAsync(
                r => r.TournamentId == request.TournamentId
                     && r.Status == RegistrationStatus.Approved
                     && r.GroupId != null,
                cancellationToken);

            var regByTeamId = allRegistrations.ToDictionary(r => r.TeamId);

            // ── Validate selections ───────────────────────────────────────────────
            var allSelectedTeamIds = request.Request.Selections
                .SelectMany(s => s.QualifiedTeamIds)
                .ToList();

            if (allSelectedTeamIds.Count != allSelectedTeamIds.Distinct().Count())
                throw new BadRequestException("لا يمكن تأهيل الفريق نفسه أكثر من مرة.");

            if (allSelectedTeamIds.Count < 2)
                throw new BadRequestException("يجب تأهيل فريقَين على الأقل للدور الإقصائي.");

            foreach (var selection in request.Request.Selections)
            {
                foreach (var teamId in selection.QualifiedTeamIds)
                {
                    if (!regByTeamId.TryGetValue(teamId, out var reg))
                        throw new NotFoundException(
                            $"الفريق {teamId} غير مسجّل في هذه البطولة أو لم يُعتمد.");

                    if (reg.GroupId != selection.GroupId)
                        throw new BadRequestException(
                            $"الفريق {teamId} لا ينتمي إلى المجموعة {selection.GroupId}.");
                }
            }

            // ── Mark qualified teams ──────────────────────────────────────────────
            var selectedSet = new HashSet<Guid>(allSelectedTeamIds);
            var toUpdate = allRegistrations
                .Where(r => selectedSet.Contains(r.TeamId))
                .ToList();

            foreach (var reg in toUpdate)
                reg.IsQualifiedForKnockout = true;

            // ── Status transition → QualificationConfirmed ────────────────────────
            tournament.ChangeStatus(TournamentStatus.QualificationConfirmed);

            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
            await _registrationRepository.UpdateRangeAsync(toUpdate, cancellationToken);

            // ── Generate knockout round 1 (automatic mode only) ──────────────────
            // In Manual mode the organiser will draw the knockout pairings
            // themselves via the manual-next-round endpoint, so we must NOT
            // auto-generate here — doing so would cause a 409 when they try.
            if (tournament.SchedulingMode != SchedulingMode.Manual)
            {
                var result = await _lifecycleService.GenerateKnockoutR1Async(
                    request.TournamentId, cancellationToken);
                return result;
            }

            // Manual mode: return a lightweight result so the frontend knows
            // qualification is confirmed and the draw button should appear.
            return new TournamentLifecycleResult
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.Name,
                CreatorUserId = tournament.CreatorUserId,
                GroupsFinished = true
            };
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
