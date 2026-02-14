using Application.DTOs.Tournaments;
using Application.Features.Tournaments.Commands.GenerateMatches;
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
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<TournamentPlayer> _tournamentPlayerRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IMediator _mediator;
    private readonly IDistributedLock _distributedLock;
    private readonly ITransactionManager _transactionManager;

    public ApproveRegistrationCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<TournamentPlayer> tournamentPlayerRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IMediator mediator,
        IDistributedLock distributedLock,
        ITransactionManager transactionManager)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _tournamentPlayerRepository = tournamentPlayerRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _mediator = mediator;
        _distributedLock = distributedLock;
        _transactionManager = transactionManager;
    }

    public async Task<TeamRegistrationDto> Handle(ApproveRegistrationCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-reg-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(10)))
        {
            throw new ConflictException("يتم معالجة عملية أخرى على هذه البطولة حالياً. يرجى المحاولة لاحقاً.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

        // Authorization
        var isOwner = tournament.CreatorUserId == request.UserId;
        var isAdmin = request.UserRole == UserRole.Admin.ToString();
        if (!isOwner && !isAdmin) throw new ForbiddenException("غير مصرح لك بإدارة طلبات هذه البطولة.");

        var registration = (await _registrationRepository.FindAsync(
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

        await _registrationRepository.UpdateAsync(registration, cancellationToken);

        // Check if all teams are approved and we reached max capacity 
        var allRegistrations = await _registrationRepository.FindAsync(r => r.TournamentId == request.TournamentId, cancellationToken);
        var activeRegistrations = allRegistrations.Where(r => r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn).ToList();
        
        if (activeRegistrations.Count == tournament.MaxTeams && activeRegistrations.All(r => r.Status == RegistrationStatus.Approved))
        {
            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (!existingMatches.Any() && tournament.Status != TournamentStatus.Active && tournament.SchedulingMode != SchedulingMode.Manual)
            {
                // Ensure data is persisted before dispatching next command so it's visible in the DB
                await _transactionManager.SaveChangesAsync(cancellationToken);

                // Dispatch command for generating matches (will handle its own transaction and notification)
                await _mediator.Send(new GenerateMatchesCommand(request.TournamentId, request.UserId, request.UserRole), cancellationToken);
            }
        }

        return _mapper.Map<TeamRegistrationDto>(registration);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
