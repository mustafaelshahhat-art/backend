using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Tournaments.Commands.SetOpeningMatch;

public class SetOpeningMatchCommandHandler : IRequestHandler<SetOpeningMatchCommand, IEnumerable<MatchDto>>
{
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly IMapper _mapper;

    public SetOpeningMatchCommandHandler(
        IRepository<Tournament> tournamentRepository,
        ITournamentLifecycleService lifecycleService,
        IMapper mapper)
    {
        _tournamentRepository = tournamentRepository;
        _lifecycleService = lifecycleService;
        _mapper = mapper;
    }

    public async Task<IEnumerable<MatchDto>> Handle(SetOpeningMatchCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _tournamentRepository.GetByIdAsync(request.TournamentId, cancellationToken);
        if (tournament == null) throw new NotFoundException(nameof(Tournament), request.TournamentId);

        // Authorization
        var isAdmin = request.UserRole == UserRole.Admin.ToString();
        var isOwner = request.UserRole == UserRole.TournamentCreator.ToString() && tournament.CreatorUserId == request.UserId;
        if (!isAdmin && !isOwner) throw new ForbiddenException("غير مصرح لك بإدارة هذه البطولة.");

        if (tournament.Status != TournamentStatus.WaitingForOpeningMatchSelection)
        {
            throw new ConflictException("لا يمكن تحديد مباراة الافتتاح في هذه المرحلة.");
        }

        tournament.OpeningMatchHomeTeamId = request.HomeTeamId;
        tournament.OpeningMatchAwayTeamId = request.AwayTeamId;

        await _tournamentRepository.UpdateAsync(tournament, cancellationToken);
        
        // Generate Matches (this adds matches to the repo)
        await _lifecycleService.GenerateKnockoutR1Async(request.TournamentId, cancellationToken);

        // Reload matches to return
        // Actually lifecycle service adds them to context.
        var tournamentWithMatches = await _tournamentRepository.GetByIdNoTrackingAsync(request.TournamentId, new[] { "Matches", "Matches.HomeTeam", "Matches.AwayTeam" }, cancellationToken);
        return _mapper.Map<IEnumerable<MatchDto>>(tournamentWithMatches?.Matches ?? new List<Match>());
    }
}
