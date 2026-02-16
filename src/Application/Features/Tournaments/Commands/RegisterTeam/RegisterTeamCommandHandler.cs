using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.RegisterTeam;

public class RegisterTeamCommandHandler : IRequestHandler<RegisterTeamCommand, TeamRegistrationDto>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<TournamentPlayer> _tournamentPlayerRepository;
    private readonly IDistributedLock _distributedLock;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _notifier;

    public RegisterTeamCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IRepository<Team> teamRepository,
        IRepository<TournamentPlayer> tournamentPlayerRepository,
        IDistributedLock distributedLock,
        IMapper mapper,
        IRealTimeNotifier notifier)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _teamRepository = teamRepository;
        _tournamentPlayerRepository = tournamentPlayerRepository;
        _distributedLock = distributedLock;
        _mapper = mapper;
        _notifier = notifier;
    }

    public async Task<TeamRegistrationDto> Handle(RegisterTeamCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-reg-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromSeconds(30), cancellationToken))
        {
            throw new ConflictException("النظام مشغول حالياً، يرجى المحاولة مرة أخرى.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, new[] { "Registrations" }, cancellationToken);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

            if (DateTime.UtcNow > tournament.RegistrationDeadline && !tournament.AllowLateRegistration)
            {
                throw new ConflictException("انتهى موعد التسجيل في البطولة.");
            }

            // ATOMIC CAPACITY CHECK — exclude rejected/withdrawn registrations
            bool isActive = tournament.Status == TournamentStatus.Active;
            int activeRegistrations = tournament.Registrations.Count(r => 
                r.Status != RegistrationStatus.Rejected && 
                r.Status != RegistrationStatus.Withdrawn);
            bool isFull = activeRegistrations >= tournament.MaxTeams;

            if (isFull && !tournament.AllowLateRegistration)
            {
                throw new ConflictException("اكتمل عدد الفرق في البطولة.");
            }

            if (isActive && !tournament.AllowLateRegistration)
            {
                throw new ConflictException("بدأت البطولة بالفعل ولا يمكن التسجيل حالياً.");
            }

            var team = await _teamRepository.GetByIdAsync(request.TeamId, new[] { "Players" }, cancellationToken);
            if (team == null) throw new NotFoundException(nameof(Team), request.TeamId);

            // Only captain can register
            if (!team.Players.Any(p => p.UserId == request.UserId && p.TeamRole == TeamRole.Captain))
            {
                throw new ForbiddenException("فقط كابتن الفريق يمكنه تسجيل الفريق في البطولات.");
            }

            if (tournament.Registrations.Any(r => r.TeamId == request.TeamId && r.Status != RegistrationStatus.Rejected && r.Status != RegistrationStatus.Withdrawn))
            {
                throw new ConflictException("الفريق مسجل بالفعل في هذه البطولة أو قيد المراجعة.");
            }

            RegistrationStatus targetStatus = RegistrationStatus.PendingPaymentReview;
            if (isActive || isFull)
            {
                if (tournament.LateRegistrationMode == LateRegistrationMode.WaitingList)
                {
                    targetStatus = RegistrationStatus.WaitingList;
                }
                else if (tournament.LateRegistrationMode == LateRegistrationMode.ReplaceIfNoMatchPlayed)
                {
                    targetStatus = RegistrationStatus.PendingPaymentReview;
                }
                else
                {
                     throw new ConflictException("التسجيل المتأخر غير متاح حالياً.");
                }
            }

            var registration = new TeamRegistration
            {
                TournamentId = request.TournamentId,
                TeamId = request.TeamId,
                Status = targetStatus
            };

            if (targetStatus != RegistrationStatus.WaitingList)
                tournament.CurrentTeams++;

            if (tournament.EntryFee <= 0 && targetStatus != RegistrationStatus.WaitingList)
            {
                // Duplicate Player Participation Prevention for auto-approval
                var playerIds = team.Players.Select(p => p.Id).ToList();
                if (playerIds.Any())
                {
                    // Check if any player in the team is already participating in this tournament via another approved registration
                    var existingParticipations = await _tournamentPlayerRepository.FindAsync(tp => tp.TournamentId == request.TournamentId && playerIds.Contains(tp.PlayerId));
                    if (existingParticipations.Any())
                    {
                        throw new ConflictException("واحد أو أكثر من اللاعبين مسجل بالفعل في فريق آخر في هذه البطولة.");
                    }
                }

                registration.Status = RegistrationStatus.Approved;
            }

            if (tournament.CurrentTeams >= tournament.MaxTeams && tournament.Status == TournamentStatus.RegistrationOpen)
            {
                tournament.ChangeStatus(TournamentStatus.RegistrationClosed);
            }

            await _registrationRepository.AddAsync(registration, cancellationToken);
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            // If auto-approved, populate TournamentPlayers
            if (registration.Status == RegistrationStatus.Approved)
            {
                var participations = team.Players
                    .Select(p => new TournamentPlayer
                    {
                        TournamentId = request.TournamentId,
                        PlayerId = p.Id,
                        TeamId = team.Id,
                        RegistrationId = registration.Id
                    }).ToList();
                
                await _tournamentPlayerRepository.AddRangeAsync(participations, cancellationToken);
            }

            var registrationDto = _mapper.Map<TeamRegistrationDto>(registration);
            
            // Notify Real-Time
            var tournamentDto = _mapper.Map<TournamentDto>(tournament);
            await _notifier.SendTournamentUpdatedAsync(tournamentDto, cancellationToken);

            return registrationDto;
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
