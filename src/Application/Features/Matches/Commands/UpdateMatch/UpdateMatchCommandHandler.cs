using Application.Common;
using Application.DTOs.Matches;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using MediatR;
using Shared.Exceptions;

namespace Application.Features.Matches.Commands.UpdateMatch;

public class UpdateMatchCommandHandler : IRequestHandler<UpdateMatchCommand, MatchDto>
{
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly IMapper _mapper;
    private readonly IRealTimeNotifier _notifier;
    private readonly IAnalyticsService _analyticsService;
    private readonly INotificationService _notificationService;

    public UpdateMatchCommandHandler(
        IRepository<Match> matchRepository,
        IRepository<Team> teamRepository,
        ITournamentLifecycleService lifecycleService,
        IMapper mapper,
        IRealTimeNotifier notifier,
        IAnalyticsService analyticsService,
        INotificationService notificationService)
    {
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _lifecycleService = lifecycleService;
        _mapper = mapper;
        _notifier = notifier;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
    }

    public async Task<MatchDto> Handle(UpdateMatchCommand request, CancellationToken cancellationToken)
    {
        // 1. Authorization
        await ValidateManagementRights(request.Id, request.UserId, request.UserRole, cancellationToken);

        var match = await _matchRepository.GetByIdAsync(request.Id, new[] { "HomeTeam", "AwayTeam" }, cancellationToken);
        if (match == null) throw new NotFoundException(nameof(Match), request.Id);

        var oldStatus = match.Status;
        bool scoreChanged = (request.Request.HomeScore.HasValue && request.Request.HomeScore != match.HomeScore) || 
                           (request.Request.AwayScore.HasValue && request.Request.AwayScore != match.AwayScore);
        
        if (request.Request.HomeScore.HasValue) match.HomeScore = request.Request.HomeScore.Value;
        if (request.Request.AwayScore.HasValue) match.AwayScore = request.Request.AwayScore.Value;
        if (request.Request.Date.HasValue) match.Date = request.Request.Date.Value;

        MatchStatus? newStatus = null;
        if (!string.IsNullOrEmpty(request.Request.Status) && Enum.TryParse<MatchStatus>(request.Request.Status, true, out var status))
        {
            newStatus = status;
            match.Status = status;
        }

        await _matchRepository.UpdateAsync(match, cancellationToken);

        // Handle Notifications & Logging
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, cancellationToken);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId, new System.Linq.Expressions.Expression<Func<Team, object>>[] { t => t.Players }, cancellationToken);

        if (scoreChanged)
        {
            await _analyticsService.LogActivityByTemplateAsync(
                ActivityConstants.MATCH_SCORE_UPDATED, 
                new Dictionary<string, string> { 
                    { "matchInfo", $"{homeTeam?.Name ?? "فريق"} ضد {awayTeam?.Name ?? "فريق"}" },
                    { "score", $"{match.HomeScore}-{match.AwayScore}" }
                }, 
                null, 
                "إدارة",
                cancellationToken
            );
        }

        // STEP 8: Trigger Standings/Lifecycle
        if (match.Status == MatchStatus.Finished || scoreChanged)
        {
             await _lifecycleService.CheckAndFinalizeTournamentAsync(match.TournamentId, cancellationToken);
        }

        // Reload to get fresh data for response
        var updatedMatch = await _matchRepository.GetByIdAsync(request.Id, new[] { "HomeTeam", "AwayTeam" }, cancellationToken);
        var matchDto = _mapper.Map<MatchDto>(updatedMatch);
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
