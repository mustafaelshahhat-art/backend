using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.GenerateManualGroupMatches;

public class GenerateManualGroupMatchesCommandHandler : IRequestHandler<GenerateManualGroupMatchesCommand, IEnumerable<MatchDto>>
{
    private readonly ITournamentService _tournamentService;
    private readonly IDistributedLock _distributedLock;

    public GenerateManualGroupMatchesCommandHandler(
        ITournamentService tournamentService,
        IDistributedLock distributedLock)
    {
        _tournamentService = tournamentService;
        _distributedLock = distributedLock;
    }

    public async Task<IEnumerable<MatchDto>> Handle(GenerateManualGroupMatchesCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("عملية جدولة أخرى قيد التنفيذ لهذا الدوري.");
        }

        try
        {
            return await _tournamentService.GenerateManualGroupMatchesAsync(request.TournamentId, request.UserId, request.UserRole, cancellationToken);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
