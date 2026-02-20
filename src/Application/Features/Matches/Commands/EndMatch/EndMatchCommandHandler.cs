using Application.Common;
using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Matches.Commands.EndMatch;

public class EndMatchCommandHandler : IRequestHandler<EndMatchCommand, MatchDto>
{
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly IMapper _mapper;
    private readonly IMatchEventNotifier _matchEventNotifier;

    public EndMatchCommandHandler(
        IRepository<Match> matchRepository,
        IRepository<Team> teamRepository,
        ITournamentLifecycleService lifecycleService,
        IMapper mapper,
        IMatchEventNotifier matchEventNotifier)
    {
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _lifecycleService = lifecycleService;
        _mapper = mapper;
        _matchEventNotifier = matchEventNotifier;
    }

    public async Task<MatchDto> Handle(EndMatchCommand request, CancellationToken cancellationToken)
    {
        await ValidateManagementRights(request.Id, request.UserId, request.UserRole, cancellationToken);
        
        var match = await _matchRepository.GetByIdAsync(request.Id, new[] { "HomeTeam", "AwayTeam", "Tournament" }, cancellationToken);
        if (match == null) throw new NotFoundException(nameof(Match), request.Id);

        match.Status = MatchStatus.Finished;
        await _matchRepository.UpdateAsync(match, cancellationToken);

        await _matchEventNotifier.LogActivityAsync(
            ActivityConstants.MATCH_ENDED, 
            new Dictionary<string, string> { 
                { "matchInfo", $"{match.HomeTeam?.Name ?? "فريق"} ضد {match.AwayTeam?.Name ?? "فريق"}" },
                { "score", $"{match.HomeScore}-{match.AwayScore}" }
            }, 
            null, 
            "نظام",
            cancellationToken
        );

        // Trigger Lifecycle check
        var lifecycleResult = await _lifecycleService.CheckAndFinalizeTournamentAsync(match.TournamentId, cancellationToken);
        await _matchEventNotifier.HandleLifecycleOutcomeAsync(lifecycleResult, cancellationToken);

        await _matchEventNotifier.SendMatchUpdateAsync(match, cancellationToken);

        return _mapper.Map<MatchDto>(match);
    }

    private async Task ValidateManagementRights(Guid matchId, Guid userId, string userRole, CancellationToken ct)
    {
        var isAdmin = userRole == UserRole.Admin.ToString();
        if (isAdmin) return;

        var match = await _matchRepository.GetByIdAsync(matchId, new[] { "Tournament" }, ct);
        if (match == null) throw new NotFoundException(nameof(Match), matchId);

        if (userRole != UserRole.TournamentCreator.ToString() || match.Tournament?.CreatorUserId != userId)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة هذا اللقاء. فقط منظم البطولة يمكنه ذلك.");
        }
    }
}
