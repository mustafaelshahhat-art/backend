using Application.DTOs.Tournaments;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.CloseRegistration;

public class CloseRegistrationCommandHandler : IRequestHandler<CloseRegistrationCommand, TournamentDto>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IDistributedLock _distributedLock;
    private readonly IMapper _mapper;

    public CloseRegistrationCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IDistributedLock distributedLock,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _distributedLock = distributedLock;
        _mapper = mapper;
    }

    public async Task<TournamentDto> Handle(CloseRegistrationCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament:lifecycle:{request.Id}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2)))
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

            // Guarded Transition
            tournament.ChangeStatus(TournamentStatus.RegistrationClosed);

            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
            
            return _mapper.Map<TournamentDto>(tournament);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
