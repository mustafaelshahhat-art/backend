using Application.DTOs;
using Application.DTOs.Matches;
using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.CreateManualKnockoutMatches;

public class CreateManualKnockoutMatchesCommandHandler : IRequestHandler<CreateManualKnockoutMatchesCommand, MatchListResponse>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IDistributedLock _distributedLock;
    private readonly IMapper _mapper;

    public CreateManualKnockoutMatchesCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Match> matchRepository,
        IDistributedLock distributedLock,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _matchRepository = matchRepository;
        _distributedLock = distributedLock;
        _mapper = mapper;
    }

    public async Task<MatchListResponse> Handle(CreateManualKnockoutMatchesCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament_scheduling_{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10), cancellationToken))
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

            if (tournament.SchedulingMode != SchedulingMode.Manual)
            {
                throw new BadRequestException("البطولة ليست في وضع الجدولة اليدوية.");
            }

            if (tournament.Format != TournamentFormat.KnockoutOnly)
            {
                throw new BadRequestException("هذا الأمر مخصص لنظام خروج المغلوب فقط.");
            }

            var matchesExist = await _matchRepository.AnyAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (matchesExist)
            {
                throw new BadRequestException("المباريات موجودة بالفعل.");
            }

            var allRegistrations = await _registrationRepository.FindAsync(
                r => r.TournamentId == request.TournamentId && 
                     r.Status == RegistrationStatus.Approved, 
                cancellationToken);

            var registeredTeamIds = allRegistrations.Select(r => r.TeamId).ToHashSet();
            var participantIds = request.Pairings.SelectMany(p => new[] { p.HomeTeamId, p.AwayTeamId }).ToList();

            if (!participantIds.All(id => registeredTeamIds.Contains(id)))
            {
                throw new BadRequestException("بعض الفرق المحددة غير مسجلة.");
            }

            if (participantIds.Count != participantIds.Distinct().Count())
            {
                throw new BadRequestException("لا يمكن تكرار الفريق في المواجهات.");
            }

            if (participantIds.Count != registeredTeamIds.Count)
            {
                throw new BadRequestException("يجب تضمين جميع الفرق المسجلة في المواجهات.");
            }

            foreach (var pairing in request.Pairings)
            {
                if (pairing.HomeTeamId == pairing.AwayTeamId)
                    throw new BadRequestException("لا يمكن للفريق أن يواجه نفسه.");
            }

            // ── Single match-generation engine ──────────────────────────────
            // MatchGenerator.GenerateManualKnockout is the same kernel used by
            // CreateManualNextRoundCommandHandler so there is ONE code path for
            // converting organiser-supplied pairings into Match entities.
            bool isHomeAway = tournament.GetEffectiveMode() == TournamentMode.KnockoutHomeAway;

            var pairingTuples = request.Pairings.Select(p =>
                (p.HomeTeamId, p.AwayTeamId, RoundNumber: 1, StageName: "Round 1"));

            var matches = MatchGenerator.GenerateManualKnockout(
                tournament.Id,
                pairingTuples,
                isHomeAway,
                tournament.StartDate.AddHours(18));

            await _matchRepository.AddRangeAsync(matches, cancellationToken);

            // Activate tournament after successful knockout pairing
            tournament.ChangeStatus(TournamentStatus.Active);
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            return new MatchListResponse(_mapper.Map<List<MatchDto>>(matches));
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
