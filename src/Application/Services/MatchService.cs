using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Matches;
using Application.Interfaces;
using Application.Common;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Shared.Exceptions;

namespace Application.Services;

public class MatchService : IMatchService
{
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<MatchEvent> _eventRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IMapper _mapper;
    private readonly IAnalyticsService _analyticsService;
    private readonly INotificationService _notificationService;
    private readonly IRealTimeNotifier _notifier;
    private readonly ITournamentLifecycleService _lifecycleService;

    public MatchService(
        IRepository<Match> matchRepository,
        IRepository<MatchEvent> eventRepository,
        IRepository<Team> teamRepository,
        IMapper mapper,
        IAnalyticsService analyticsService,
        INotificationService notificationService,
        IRealTimeNotifier notifier,
        ITournamentLifecycleService lifecycleService)
    {
        _matchRepository = matchRepository;
        _eventRepository = eventRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
        _notifier = notifier;
        _lifecycleService = lifecycleService;
    }

    public async Task<IEnumerable<MatchDto>> GetAllAsync()
    {
        var matches = await _matchRepository.GetAllAsync(new[] { "HomeTeam", "AwayTeam", "Events.Player" });
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    public async Task<MatchDto?> GetByIdAsync(Guid id)
    {
        var match = await _matchRepository.GetByIdAsync(id, new[] { "HomeTeam", "AwayTeam", "Events.Player" });
        if (match == null) return null;

        if (match.Events == null || !match.Events.Any())
        {
             var events = await _eventRepository.FindAsync(e => e.MatchId == id, new[] { "Player" });
             match.Events = events.ToList();
        }

        return _mapper.Map<MatchDto>(match);
    }

    public async Task<MatchDto> StartMatchAsync(Guid id, Guid userId, string userRole)
    {
        await ValidateManagementRights(id, userId, userRole);
        var match = await _matchRepository.GetByIdAsync(id);
        if (match == null) throw new NotFoundException(nameof(Match), id);

        match.Status = MatchStatus.Live;
        await _matchRepository.UpdateAsync(match);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.MATCH_STARTED, 
            new Dictionary<string, string> { { "matchInfo", $"{match.HomeTeam?.Name ?? "فريق"} ضد {match.AwayTeam?.Name ?? "فريق"}" } }, 
            null, 
            "نظام"
        );
        
        // Lightweight System Event
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = MatchStatus.Live.ToString() }, $"match:{id}");
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = MatchStatus.Live.ToString() }, "role:Admin");
        
        // Notify Captains (requires fetching teams to get CaptainId)
        // Optimization: Fetch match with Teams included or fetch Teams separately.
        // Assuming match has HomeTeamId/AwayTeamId but usually not loaded.
        // Let's fetch teams.
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId, t => t.Players);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId, t => t.Players);

        if (homeTeam != null)
        {
            var captain = homeTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_STARTED, new Dictionary<string, string> { { "opponent", awayTeam?.Name ?? "الخصم" } }, "match");
            }
        }
        
        if (awayTeam != null)
        {
            var captain = awayTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_STARTED, new Dictionary<string, string> { { "opponent", homeTeam?.Name ?? "الخصم" } }, "match");
            }
        }

        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }

    public async Task<MatchDto> EndMatchAsync(Guid id, Guid userId, string userRole)
    {
        await ValidateManagementRights(id, userId, userRole);
        var match = await _matchRepository.GetByIdAsync(id);
        if (match == null) throw new NotFoundException(nameof(Match), id);

        match.Status = MatchStatus.Finished;
        await _matchRepository.UpdateAsync(match);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.MATCH_ENDED, 
            new Dictionary<string, string> { 
                { "matchInfo", $"{match.HomeTeam?.Name ?? "فريق"} ضد {match.AwayTeam?.Name ?? "فريق"}" },
                { "score", $"{match.HomeScore}-{match.AwayScore}" }
            }, 
            null, 
            "نظام"
        );

        // Lightweight System Event
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = MatchStatus.Finished.ToString() }, $"match:{id}");
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = MatchStatus.Finished.ToString() }, "role:Admin");

        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId, t => t.Players);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId, t => t.Players);

        if (homeTeam != null)
        {
            var captain = homeTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_ENDED, new Dictionary<string, string> { { "opponent", awayTeam?.Name ?? "الخصم" }, { "score", $"{match.HomeScore}-{match.AwayScore}" } }, "match");
            }
        }

        if (awayTeam != null)
        {
            var captain = awayTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_ENDED, new Dictionary<string, string> { { "opponent", homeTeam?.Name ?? "الخصم" }, { "score", $"{match.HomeScore}-{match.AwayScore}" } }, "match");
            }
        }

        // Trigger Lifecycle check
        await _lifecycleService.CheckAndFinalizeTournamentAsync(match.TournamentId);

        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }

    public async Task<MatchDto> AddEventAsync(Guid id, AddMatchEventRequest request, Guid userId, string userRole)
    {
        await ValidateManagementRights(id, userId, userRole);
        var match = await _matchRepository.GetByIdAsync(id);
        if (match == null) throw new NotFoundException(nameof(Match), id);

        // Convert string Type to Enum
        if (!Enum.TryParse<MatchEventType>(request.Type, true, out var eventType))
        {
            throw new BadRequestException("Invalid event type.");
        }

        var matchEvent = new MatchEvent
        {
            MatchId = id,
            TeamId = request.TeamId,
            PlayerId = request.PlayerId,
            Type = eventType,
            Minute = request.Minute
        };

        if (eventType == MatchEventType.Goal)
        {
            if (request.TeamId == match.HomeTeamId)
                match.HomeScore++;
            else if (request.TeamId == match.AwayTeamId)
                match.AwayScore++;
            
            
            await _matchRepository.UpdateAsync(match);
            await _analyticsService.LogActivityByTemplateAsync(
                ActivityConstants.MATCH_EVENT_ADDED, 
                new Dictionary<string, string> { 
                    { "eventType", "هدف" },
                    { "playerName", request.PlayerId?.ToString() ?? "لاعب" },
                    { "matchInfo", id.ToString() }
                }, 
                request.PlayerId, 
                "لاعب"
            );
        }

        await _eventRepository.AddAsync(matchEvent);
        
        // Refresh events for clean return
        var events = await _eventRepository.FindAsync(e => e.MatchId == id, new[] { "Player" });
        match.Events = events.ToList();

        // Notify
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId, t => t.Players);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId, t => t.Players);
        string eventLabel = eventType == MatchEventType.Goal ? "هدف" : eventType == MatchEventType.YellowCard ? "بطاقة صفراء" : "بطاقة حمراء";
        
        var placeholders = new Dictionary<string, string> { { "eventType", eventLabel } };
        
        if (homeTeam != null)
        {
            var captain = homeTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_EVENT_ADDED, placeholders, "match");
            }
        }

        if (awayTeam != null)
        {
            var captain = awayTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_EVENT_ADDED, placeholders, "match");
            }
        }


        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }

    public async Task<MatchDto> RemoveEventAsync(Guid matchId, Guid eventId, Guid userId, string userRole)
    {
        await ValidateManagementRights(matchId, userId, userRole);
        var match = await _matchRepository.GetByIdAsync(matchId);
        if (match == null) throw new NotFoundException(nameof(Match), matchId);

        var matchEvent = await _eventRepository.GetByIdAsync(eventId);
        if (matchEvent == null || matchEvent.MatchId != matchId) throw new NotFoundException(nameof(MatchEvent), eventId);

        // Rollback effects
        if (matchEvent.Type == MatchEventType.Goal)
        {
            if (matchEvent.TeamId == match.HomeTeamId)
                match.HomeScore = Math.Max(0, match.HomeScore - 1);
            else if (matchEvent.TeamId == match.AwayTeamId)
                match.AwayScore = Math.Max(0, match.AwayScore - 1);
            
            await _matchRepository.UpdateAsync(match);
        }

        await _eventRepository.DeleteAsync(matchEvent);
        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.MATCH_EVENT_REMOVED, 
            new Dictionary<string, string> { 
                { "matchInfo", matchId.ToString() }
            }, 
            null, 
            "إدارة"
        );

        // Notify
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId, t => t.Players);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId, t => t.Players);
        var placeholders = new Dictionary<string, string> { { "eventType", "تعديل في الأحداث" } };
        if (homeTeam != null)
        {
            var captain = homeTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_EVENT_ADDED, placeholders, "match");
            }
        }

        if (awayTeam != null)
        {
            var captain = awayTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
            if (captain != null && captain.UserId.HasValue)
            {
                await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_EVENT_ADDED, placeholders, "match");
            }
        }


        // Refresh events
        var events = await _eventRepository.FindAsync(e => e.MatchId == matchId, new[] { "Player" });
        match.Events = events.ToList();

        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }



    public async Task<MatchDto> UpdateAsync(Guid id, UpdateMatchRequest request, Guid userId, string userRole)
    {
        await ValidateManagementRights(id, userId, userRole);
        var match = await _matchRepository.GetByIdAsync(id, new[] { "HomeTeam", "AwayTeam" });
        if (match == null) throw new NotFoundException(nameof(Match), id);


        var oldStatus = match.Status;
        var oldScore = $"{match.HomeScore}-{match.AwayScore}";
        var oldDate = match.Date;

        bool scoreChanged = (request.HomeScore.HasValue && request.HomeScore != match.HomeScore) || 
                           (request.AwayScore.HasValue && request.AwayScore != match.AwayScore);
        
        if (request.HomeScore.HasValue) match.HomeScore = request.HomeScore.Value;
        if (request.AwayScore.HasValue) match.AwayScore = request.AwayScore.Value;
        if (request.Date.HasValue) match.Date = request.Date.Value;

        
        MatchStatus? newStatus = null;
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<MatchStatus>(request.Status, true, out var status))
        {
            newStatus = status;
            match.Status = status;
        }

        await _matchRepository.UpdateAsync(match);

        // Handle Notifications & Logging
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId, t => t.Players);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId, t => t.Players);

        // 1. Score Update
        if (scoreChanged)
        {
            await _analyticsService.LogActivityByTemplateAsync(
                ActivityConstants.MATCH_SCORE_UPDATED, 
                new Dictionary<string, string> { 
                    { "matchInfo", $"{homeTeam?.Name ?? "فريق"} ضد {awayTeam?.Name ?? "فريق"}" },
                    { "score", $"{match.HomeScore}-{match.AwayScore}" }
                }, 
                null, 
                "إدارة"
            );
            var scoreStr = $"{match.HomeScore}-{match.AwayScore}";
            
            if (homeTeam != null)
            {
                var captain = homeTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
                if (captain != null && captain.UserId.HasValue)
                {
                    await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_SCORE_CHANGED, new Dictionary<string, string> { { "opponent", awayTeam?.Name ?? "الخصم" }, { "score", scoreStr } }, "match");
                }
            }

            if (awayTeam != null)
            {
                var captain = awayTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
                if (captain != null && captain.UserId.HasValue)
                {
                    await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, NotificationTemplates.MATCH_SCORE_CHANGED, new Dictionary<string, string> { { "opponent", homeTeam?.Name ?? "الخصم" }, { "score", scoreStr } }, "match");
                }
            }

        }



        // 3. Status Changes (Postpone, Cancel, Reschedule)
        if (newStatus.HasValue && newStatus.Value != oldStatus)
        {
            string templateKey = "";
            var placeholders = new Dictionary<string, string> { 
                { "opponent", "الخصم" }, 
                { "date", $"{match.Date:yyyy/MM/dd} {match.Date:HH:mm}" } 
            };

            switch (newStatus.Value)
            {
                case MatchStatus.Postponed:
                    templateKey = NotificationTemplates.MATCH_POSTPONED;
                    break;
                case MatchStatus.Cancelled:
                    templateKey = NotificationTemplates.MATCH_CANCELED;
                    break;
                case MatchStatus.Rescheduled:
                    templateKey = NotificationTemplates.MATCH_TIME_CHANGED;
                    break;
            }

            if (!string.IsNullOrEmpty(templateKey))
            {
                if (homeTeam != null) {
                var captain = homeTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
                if (captain != null && captain.UserId.HasValue)
                {
                    placeholders["opponent"] = awayTeam?.Name ?? "الخصم";
                    await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, templateKey, placeholders, "match");
                }
            }
                if (awayTeam != null) {
                 var captain = awayTeam.Players.FirstOrDefault(p => p.TeamRole == TeamRole.Captain);
                 if (captain != null && captain.UserId.HasValue)
                 {
                     placeholders["opponent"] = homeTeam?.Name ?? "الخصم";
                     await _notificationService.SendNotificationByTemplateAsync(captain.UserId.Value, templateKey, placeholders, "match");
                 }
            }

            }
      // Lightweight System Events
            if (newStatus == MatchStatus.Postponed)
                await _notifier.SendSystemEventAsync("MATCH_RESCHEDULED", new { MatchId = id, Date = match.Date }, $"match:{id}");
            else
                await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = newStatus.Value.ToString() }, $"match:{id}");
            
            await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = newStatus.Value.ToString() }, "role:Admin");
        }

        // 4. Trigger Tournament Lifecycle check just in case
        await _lifecycleService.CheckAndFinalizeTournamentAsync(match.TournamentId);

        // Reload to get fresh data
        match = await _matchRepository.GetByIdAsync(id, new[] { "HomeTeam", "AwayTeam" });
        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }

    public async Task<IEnumerable<MatchDto>> GenerateMatchesForTournamentAsync(Guid tournamentId)
    {
        // Check if matches already exist for this tournament
        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        if (existingMatches.Any())
        {
            throw new ConflictException("المباريات موجودة بالفعل لهذه البطولة.");
        }

        // Get all approved/pending registrations for this tournament
        // We need to access registrations - inject repository
        // For now, let's assume we get team IDs from a separate call
        // Actually, we need to get the teams - let me fetch all teams registered
        
        var allTeams = await _teamRepository.GetAllAsync();
        // We need to filter by tournament registrations - this requires access to registration repository
        // Since we don't have it injected, let's add a workaround by having the caller pass team IDs
        // OR we can add the repository. For simplicity, let's assume all teams in allTeams are for this tournament
        // This is a placeholder - real implementation should filter by tournament registrations
        
        throw new NotImplementedException("This method requires tournament registration data. Use GenerateMatchesAsync with team IDs.");
    }

    public async Task<IEnumerable<MatchDto>> GenerateMatchesAsync(Guid tournamentId, List<Guid> teamIds)
    {
        // Check if matches already exist
        var existingMatches = await _matchRepository.FindAsync(m => m.TournamentId == tournamentId);
        if (existingMatches.Any())
        {
            throw new ConflictException("المباريات موجودة بالفعل لهذه البطولة.");
        }

        if (teamIds.Count < 2)
        {
            throw new BadRequestException("يجب وجود فريقين على الأقل لتوليد المباريات.");
        }

        var matches = new List<Match>();
        var random = new Random();
        
        // Shuffle teams for random order
        var shuffledTeams = teamIds.OrderBy(x => random.Next()).ToList();
        
        // Round Robin: Each team plays against every other team once
        var matchDate = DateTime.UtcNow.AddDays(7); // First match week from now
        var matchNumber = 0;
        
        for (int i = 0; i < shuffledTeams.Count; i++)
        {
            for (int j = i + 1; j < shuffledTeams.Count; j++)
            {
                var match = new Match
                {
                    TournamentId = tournamentId,
                    HomeTeamId = shuffledTeams[i],
                    AwayTeamId = shuffledTeams[j],
                    Status = MatchStatus.Scheduled,
                    Date = matchDate.AddDays(matchNumber * 3), // 3 days between matches
                    HomeScore = 0,
                    AwayScore = 0
                };
                
                matches.Add(match);
                matchNumber++;
            }
        }

        // Save all matches
        foreach (var match in matches)
        {
            await _matchRepository.AddAsync(match);
        }

        await _analyticsService.LogActivityByTemplateAsync(
            ActivityConstants.TOURNAMENT_GENERATED, 
            new Dictionary<string, string> { 
                { "tournamentName", tournamentId.ToString() } 
            }, 
            null, 
            "نظام"
        );

        var matchDtos = _mapper.Map<IEnumerable<MatchDto>>(matches);
        await _notifier.SendMatchesGeneratedAsync(matchDtos);

        return matchDtos;
    }

    private async Task ValidateManagementRights(Guid matchId, Guid userId, string userRole)
    {
        var isAdmin = userRole == UserRole.Admin.ToString();
        if (isAdmin) return;

        var match = await _matchRepository.GetByIdAsync(matchId, new[] { "Tournament" });
        if (match == null) throw new NotFoundException(nameof(Match), matchId);

        if (userRole != UserRole.TournamentCreator.ToString() || match.Tournament?.CreatorUserId != userId)
        {
             throw new ForbiddenException("غير مصرح لك بإدارة هذا اللقاء. فقط منظم البطولة يمكنه ذلك.");
        }
    }
}
