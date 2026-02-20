using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.ApproveRegistration;

public class ApproveRegistrationCommandHandler : IRequestHandler<ApproveRegistrationCommand, TeamRegistrationDto>
{
    private readonly ITournamentRegistrationContext _regContext;
    private readonly IRepository<TournamentPlayer> _tournamentPlayerRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly ITransactionManager _transactionManager;

    public ApproveRegistrationCommandHandler(
        ITournamentRegistrationContext regContext,
        IRepository<TournamentPlayer> tournamentPlayerRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        ITransactionManager transactionManager)
    {
        _regContext = regContext;
        _tournamentPlayerRepository = tournamentPlayerRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _transactionManager = transactionManager;
    }

    public async Task<TeamRegistrationDto> Handle(ApproveRegistrationCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-reg-{request.TournamentId}";
        if (!await _regContext.DistributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10)))
        {
            throw new ConflictException("يتم معالجة عملية أخرى على هذه البطولة حالياً. يرجى المحاولة لاحقاً.");
        }

        try
        {
            var tournament = await _regContext.Tournaments.GetByIdAsync(request.TournamentId, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

        // Authorization
        var isOwner = tournament.CreatorUserId == request.UserId;
        var isAdmin = request.UserRole == UserRole.Admin.ToString();
        if (!isOwner && !isAdmin) throw new ForbiddenException("غير مصرح لك بإدارة طلبات هذه البطولة.");

        var registration = (await _regContext.Registrations.FindAsync(
            r => r.TournamentId == request.TournamentId && r.TeamId == request.TeamId, 
            new[] { "Team", "Team.Players" }, 
            cancellationToken)).FirstOrDefault();
        
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        if (registration.Status != RegistrationStatus.PendingPaymentReview && registration.Status != RegistrationStatus.WaitingList)
        {
            throw new ConflictException($"لا يمكن اعتماد الطلب. الحالة الحالية: {registration.Status}");
        }

        // Duplicate Player Participation Prevention
        var playerIds = registration.Team?.Players.Select(p => p.Id).ToList() ?? new List<Guid>();
        if (playerIds.Any())
        {
            var existingParticipations = await _tournamentPlayerRepository.FindAsync(tp => tp.TournamentId == request.TournamentId && playerIds.Contains(tp.PlayerId));
            if (existingParticipations.Any())
            {
                throw new ConflictException("واحد أو أكثر من اللاعبين مسجل بالفعل في فريق آخر في هذه البطولة.");
            }
        }

        registration.Status = RegistrationStatus.Approved;
        
        // Populate TournamentPlayers tracking
        if (registration.Team?.Players != null)
        {
            var participations = registration.Team.Players
                .Select(p => new TournamentPlayer
                {
                    TournamentId = request.TournamentId,
                    PlayerId = p.Id,
                    TeamId = registration.TeamId,
                    RegistrationId = registration.Id
                }).ToList();
            
            await _tournamentPlayerRepository.AddRangeAsync(participations, cancellationToken);
        }

        await _regContext.Registrations.UpdateAsync(registration, cancellationToken);

        // Check if all teams are approved and we reached max capacity 
        var allRegistrations = await _regContext.Registrations.FindAsync(r => r.TournamentId == request.TournamentId, cancellationToken);
        var activeRegistrations = allRegistrations.Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn).ToList();
        
        if (activeRegistrations.Count == tournament.MaxTeams && activeRegistrations.All(r => r.Status == RegistrationStatus.Approved))
        {
            // STRICT MODE: Do NOT auto-generate matches.
            // Transition to WaitingForOpeningMatchSelection instead.
            if (tournament.Status != TournamentStatus.Active && tournament.Status != TournamentStatus.WaitingForOpeningMatchSelection &&
                tournament.SchedulingMode != SchedulingMode.Manual)
            {
                tournament.ChangeStatus(TournamentStatus.WaitingForOpeningMatchSelection);
                await _regContext.Tournaments.UpdateAsync(tournament, cancellationToken);
            }
        }

        return _mapper.Map<TeamRegistrationDto>(registration);
        }
        finally
        {
            await _regContext.DistributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
