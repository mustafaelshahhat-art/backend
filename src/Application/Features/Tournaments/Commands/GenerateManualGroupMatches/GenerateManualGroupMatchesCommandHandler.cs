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

namespace Application.Features.Tournaments.Commands.GenerateManualGroupMatches;

public class GenerateManualGroupMatchesCommandHandler : IRequestHandler<GenerateManualGroupMatchesCommand, MatchListResponse>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedLock _distributedLock;

    public GenerateManualGroupMatchesCommandHandler(
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IMapper mapper,
        IDistributedLock distributedLock)
    {
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
        _distributedLock = distributedLock;
    }

    public async Task<MatchListResponse> Handle(GenerateManualGroupMatchesCommand request, CancellationToken cancellationToken)
    {
        var lockKey = $"tournament-lock-{request.TournamentId}";
        if (!await _distributedLock.AcquireLockAsync(lockKey, TimeSpan.FromMinutes(2), cancellationToken))
        {
            throw new ConflictException("عملية جدولة أخرى قيد التنفيذ لهذا الدوري.");
        }

        try
        {
            var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, new[] { "Registrations" }, cancellationToken);
            if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

            TournamentHelper.ValidateOwnership(tournament, request.UserId, request.UserRole);

            if (tournament.SchedulingMode != SchedulingMode.Manual)
                throw new BadRequestException("البطولة ليست في وضع الجدولة اليدوية.");

            var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == request.TournamentId, cancellationToken);
            if (existingMatches.Any()) throw new ConflictException("المباريات مولدة بالفعل.");

            var registrations = tournament.Registrations.Where(r => r.Status == RegistrationStatus.Approved).ToList();
            if (registrations.Any(r => r.GroupId == null))
                throw new BadRequestException("لم يتم تعيين جميع الفرق للمجموعات.");

            var matches = TournamentHelper.CreateManualGroupMatches(tournament);

            await _matchRepository.AddRangeAsync(matches);

            tournament.ChangeStatus(TournamentStatus.Active);
            await _tournamentRepository.UpdateAsync(tournament, cancellationToken);

            return new MatchListResponse(_mapper.Map<List<MatchDto>>(matches));
        }
        finally
        {
            await _distributedLock.ReleaseLockAsync(lockKey);
        }
    }
}
