using Application.DTOs;
using Application.DTOs.Matches;
using Application.Features.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.GenerateMatches;

public class GenerateMatchesCommandHandler : IRequestHandler<GenerateMatchesCommand, MatchListResponse>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _notifier;
    private readonly IDistributedLock _distributedLock;

    public GenerateMatchesCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IRealTimeNotifier notifier,
        IDistributedLock distributedLock)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _notifier = notifier;
        _distributedLock = distributedLock;
    }

    public async Task<MatchListResponse> Handle(GenerateMatchesCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("عملية إنشاء المباريات قيد التنفيذ بالفعل.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, new[] { "Registrations", "Registrations.Team" }, cancellationToken);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

            TournamentHelper.ValidateOwnership(tournament, request.UserId, request.UserRole);

            if (tournament.Status != TournamentStatus.RegistrationClosed && tournament.Status != TournamentStatus.WaitingForOpeningMatchSelection)
                throw new ConflictException("يجب إغلاق التسجيل قبل إنشاء المباريات.");

            if (tournament.SchedulingMode == SchedulingMode.Manual)
                throw new BadRequestException("لا يمكن استخدام التوليد التلقائي في وضع الجدولة اليدوية.");

            if (tournament.SchedulingMode == SchedulingMode.Random && !tournament.HasOpeningTeams)
                throw new ConflictException("يجب اختيار الفريقين للمباراة الافتتاحية قبل توليد المباريات في الوضع العشوائي.");

            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل.");

            var hasPendingPayments = tournament.Registrations.Any(r =>
                r.Status == RegistrationStatus.PendingPaymentReview ||
                r.Status == RegistrationStatus.PendingPayment);

            if (hasPendingPayments)
            {
                throw new ConflictException("لا يمكن توليد المباريات. بانتظار اكتمال الموافقة على جميع المدفوعات.");
            }

            var registrations = tournament.Registrations.Where(r => r.Status == RegistrationStatus.Approved).ToList();
            int minRequired = tournament.MinTeams ?? 2;
            if (registrations.Count < minRequired)
                throw new ConflictException($"عدد الفرق غير كافٍ. المطلوب {minRequired} فريق على الأقل.");

            var teamIds = registrations.Select(r => r.TeamId).ToList();
            var matches = TournamentHelper.CreateMatches(tournament, teamIds);

            await _matchRepository.AddRangeAsync(matches);

            tournament.ChangeStatus(TournamentStatus.Active);
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            var dtos = _mapper.Map<IEnumerable<MatchDto>>(matches);
            await _notifier.SendMatchesGeneratedAsync(dtos, cancellationToken);

            return new MatchListResponse(dtos.ToList());
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
