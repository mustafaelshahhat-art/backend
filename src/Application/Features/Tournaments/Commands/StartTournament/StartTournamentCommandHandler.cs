using Application.DTOs.Tournaments;
using Application.Features.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.StartTournament;

public class StartTournamentCommandHandler : IRequestHandler<StartTournamentCommand, TournamentDto>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IDistributedLock _distributedLock;
    private readonly IMapper _mapper;

    public StartTournamentCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IRepository<TeamRegistration> registrationRepository,
        IDistributedLock distributedLock,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _registrationRepository = registrationRepository;
        _distributedLock = distributedLock;
        _mapper = mapper;
    }

    public async Task<TournamentDto> Handle(StartTournamentCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.Id}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("العملية قيد التنفيذ من قبل مستخدم آخر.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.Id, cancellationToken);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), request.Id);

            // Authorization
            if (request.UserRole != UserRole.Admin.ToString() && tournament.CreatorUserId != request.UserId)
            {
                throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");
            }

            // Validation 1: Registration must be closed
            if (tournament.Status != TournamentStatus.RegistrationClosed)
            {
                throw new ConflictException("يجب إغلاق التسجيل أولاً.");
            }

            // Validation 2: Minimum teams met
            var registrations = await _registrationRepository.FindAsync(
                r => r.TournamentId == request.Id && r.Status == RegistrationStatus.Approved, 
                cancellationToken);
            var teamCount = registrations.Count();
            var minRequired = tournament.MinTeams ?? 2;
            if (teamCount < minRequired)
            {
                throw new ConflictException($"عدد الفرق غير كافٍ. المطلوب {minRequired} فريق على الأقل.");
            }

            // Validation 3: Matches must exist for Random mode, but not required for Manual mode
            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.Id, cancellationToken);
            if (!existingMatches.Any())
            {
                if (tournament.SchedulingMode == SchedulingMode.Random)
                {
                    // Reload with includes for match generation
                    var freshTournament = await _tournamentRepository.GetByIdAsync(request.Id, new[] { "Registrations", "Registrations.Team" }, cancellationToken);
                    if (freshTournament == null) throw new NotFoundException(nameof(Tournament), request.Id);

                    var teamIds = registrations.Select(r => r.TeamId).ToList();
                    var matches = TournamentHelper.CreateMatches(freshTournament, teamIds);
                    await _matchRepository.AddRangeAsync(matches);
                    // Update tournament with any group assignments
                    await _tournamentRepository.UpdateAsync(freshTournament, cancellationToken);
                    // Point tournament to freshTournament for subsequent checks
                    tournament = freshTournament;
                }
                // For Manual mode, matches don't need to exist yet - they will be generated after manual draw
            }

            // Refresh existingMatches after generation for subsequent validations
            existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.Id, cancellationToken);

            // Validation 4: Opening match integrity
            if (tournament.HasOpeningTeams)
            {
                var openingMatch = existingMatches.FirstOrDefault(m => m.IsOpeningMatch);
                if (openingMatch == null)
                {
                    throw new ConflictException("تم تحديد فريقي الافتتاح لكن لم يتم إنشاء مباراة الافتتاح. أعد توليد الجدول.");
                }

                var openingTeams = new HashSet<Guid> { tournament.OpeningTeamAId!.Value, tournament.OpeningTeamBId!.Value };
                var matchTeams = new HashSet<Guid> { openingMatch.HomeTeamId, openingMatch.AwayTeamId };
                if (!openingTeams.SetEquals(matchTeams))
                {
                    throw new ConflictException("مباراة الافتتاح لا تحتوي على الفريقين المحددين. أعد توليد الجدول.");
                }
            }
            else
            {
                var effectiveMode = tournament.GetEffectiveMode();
                if (effectiveMode == TournamentMode.KnockoutSingle || 
                    effectiveMode == TournamentMode.KnockoutHomeAway ||
                    effectiveMode == TournamentMode.GroupsKnockoutSingle ||
                    effectiveMode == TournamentMode.GroupsKnockoutHomeAway)
                {
                    if (!tournament.OpeningMatchHomeTeamId.HasValue || !tournament.OpeningMatchAwayTeamId.HasValue)
                    {
                        throw new ConflictException("يجب اختيار مباراة الافتتاح أولاً.");
                    }
                }
            }

            // Execute: Change status to Active
            tournament.ChangeStatus(TournamentStatus.Active);
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            return _mapper.Map<TournamentDto>(tournament);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
