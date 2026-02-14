using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.PromoteWaitingTeam;

public class PromoteWaitingTeamCommandHandler : IRequestHandler<PromoteWaitingTeamCommand, TeamRegistrationDto>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedLock _distributedLock;

    public PromoteWaitingTeamCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<TeamRegistration> registrationRepository,
        IMapper mapper,
        IDistributedLock distributedLock)
    {
        _tournamentRepository = tournamentRepository;
        _registrationRepository = registrationRepository;
        _mapper = mapper;
        _distributedLock = distributedLock;
    }

    public async Task<TeamRegistrationDto> Handle(PromoteWaitingTeamCommand request, CancellationToken cancellationToken)
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
            var isAdmin = request.UserRole == UserRole.Admin.ToString();
            var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
            if (!isAdmin && !isOwner) throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

            var registration = (await _registrationRepository.FindAsync(r => r.TournamentId == request.TournamentId && r.TeamId == request.TeamId, cancellationToken)).FirstOrDefault();
            if (registration == null) throw new NotFoundException("التسجيل غير موجود.");

            if (registration.Status != RegistrationStatus.WaitingList)
            {
                throw new ConflictException("الفريق ليس في قائمة الانتظار.");
            }

            if (tournament.CurrentTeams >= tournament.MaxTeams)
            {
                throw new ConflictException("البطولة مكتملة العدد بالفعل.");
            }

            registration.Status = RegistrationStatus.PendingPaymentReview;
            tournament.CurrentTeams++;

            await _registrationRepository.UpdateAsync(registration, cancellationToken);
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            return _mapper.Map<TeamRegistrationDto>(registration);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
