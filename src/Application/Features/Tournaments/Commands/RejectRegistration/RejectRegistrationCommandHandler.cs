using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.RejectRegistration;

public class RejectRegistrationCommandHandler : IRequestHandler<RejectRegistrationCommand, TeamRegistrationDto>
{
    private readonly ITournamentRegistrationContext _regContext;
    private readonly IRepository<TournamentPlayer> _tournamentPlayerRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRealTimeNotifier _notifier;
    private readonly IMapper _mapper;

    public RejectRegistrationCommandHandler(
        ITournamentRegistrationContext regContext,
        IRepository<TournamentPlayer> tournamentPlayerRepository,
        IRepository<Match> matchRepository,
        IRealTimeNotifier notifier,
        IMapper mapper)
    {
        _regContext = regContext;
        _tournamentPlayerRepository = tournamentPlayerRepository;
        _matchRepository = matchRepository;
        _notifier = notifier;
        _mapper = mapper;
    }

    public async Task<TeamRegistrationDto> Handle(RejectRegistrationCommand request, CancellationToken cancellationToken)
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

        // Authorization: Tournament Creator or Admin
        var isOwner = tournament.CreatorUserId == request.UserId;
        var isAdmin = request.UserRole == UserRole.Admin.ToString();

        if (!isOwner && !isAdmin)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة طلبات هذه البطولة. فقط منظم البطولة أو مدير النظام يمكنه ذلك.");
        }

        var registration = (await _regContext.Registrations.FindAsync(r => r.TournamentId == request.TournamentId && r.TeamId == request.TeamId, new[] { "Team", "Team.Players" }, cancellationToken)).FirstOrDefault();
        if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

        // State Validation: Must be Pending
        if (registration.Status != RegistrationStatus.PendingPaymentReview)
        {
             throw new ConflictException($"لا يمكن رفض الطلب. الحالة الحالية: {registration.Status}");
        }

        registration.Status = RegistrationStatus.Rejected;
        registration.RejectionReason = request.Request.Reason;
        
        // Use Domain Event for reliable processing via Outbox
        var captain = registration.Team?.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
        if (captain?.UserId != null)
        {
            registration.AddDomainEvent(new Domain.Events.TournamentRegistrationRejectedEvent(
                request.TournamentId,
                request.TeamId,
                captain.UserId.Value,
                tournament.Name,
                registration.Team?.Name ?? "فريق",
                request.Request.Reason
            ));
        }

        await _regContext.Registrations.UpdateAsync(registration, cancellationToken);

        // Cleanup TournamentPlayers
        var participations = await _tournamentPlayerRepository.FindAsync(tp => tp.RegistrationId == registration.Id, cancellationToken);
        await _tournamentPlayerRepository.DeleteRangeAsync(participations, cancellationToken);
        
        if (tournament.CurrentTeams > 0)
        {
            tournament.CurrentTeams--;
            
            // Re-open registration if it was closed due to capacity but no matches exist yet
            if (tournament.Status == TournamentStatus.RegistrationClosed && tournament.CurrentTeams < tournament.MaxTeams)
            {
                var matches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
                if (!matches.Any() && DateTime.UtcNow <= tournament.RegistrationDeadline)
                {
                    tournament.ChangeStatus(TournamentStatus.RegistrationOpen);
                }
            }
            
            await _regContext.Tournaments.UpdateAsync(tournament, cancellationToken);
        }

        // Notify Real-Time (Fresh read after save)
        var updatedTournament = await _regContext.Tournaments.GetByIdAsync(request.TournamentId, cancellationToken);
        if (updatedTournament != null)
        {
            var tournamentDto = _mapper.Map<TournamentDto>(updatedTournament);
            await _notifier.SendTournamentUpdatedAsync(tournamentDto, cancellationToken);
        }

        return _mapper.Map<TeamRegistrationDto>(registration);
        }
        finally
        {
            await _regContext.DistributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
