using Application.Common;
using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Matches.Commands.StartMatch;

public class StartMatchCommandHandler : IRequestHandler<StartMatchCommand, MatchDto>
{
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _notifier;
    private readonly IAnalyticsService _analyticsService;
    private readonly INotificationService _notificationService;

    public StartMatchCommandHandler(
        IRepository<Match> matchRepository,
        IRepository<Team> teamRepository,
        IMapper mapper,
        IRealTimeNotifier notifier,
        IAnalyticsService analyticsService,
        INotificationService notificationService)
    {
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
        _notifier = notifier;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
    }

    public async Task<MatchDto> Handle(StartMatchCommand request, CancellationToken cancellationToken)
    {
        await ValidateManagementRights(request.Id, request.UserId, request.UserRole, cancellationToken);
        
        var match = await _matchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (match == null) throw new NotFoundException(nameof(Match), request.Id);

        match.Status = MatchStatus.Live;
        await _matchRepository.UpdateAsync(match, cancellationToken);

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.MATCH_STARTED, 
            new Dictionary<string, string> { { "matchInfo", $"{match.HomeTeam?.Name ?? "فريق"} ضد {match.AwayTeam?.Name ?? "فريق"}" } }, 
            null, 
            "نظام",
            cancellationToken
        );

        // Notify
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = request.Id, Status = MatchStatus.Live.ToString() }, $"match:{request.Id}", cancellationToken);
        
        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto, cancellationToken);

        return matchDto;
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
