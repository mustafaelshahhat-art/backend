using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.GenerateMatches;

public class GenerateMatchesCommandHandler : IRequestHandler<GenerateMatchesCommand, IEnumerable<MatchDto>>
{
    private readonly ITournamentService _tournamentService;
    private readonly IDistributedLock _distributedLock;

    public GenerateMatchesCommandHandler(
        ITournamentService tournamentService,
        IDistributedLock distributedLock)
    {
        _tournamentService = tournamentService;
        _distributedLock = distributedLock;
    }

    public async Task<IEnumerable<MatchDto>> Handle(GenerateMatchesCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("عملية إنشاء المباريات قيد التنفيذ بالفعل.");
        }

        try
        {
            return await _tournamentService.GenerateMatchesAsync(request.TournamentId, request.UserId, request.UserRole, cancellationToken);
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }
}
