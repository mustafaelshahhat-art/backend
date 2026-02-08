using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Matches;
using Application.Interfaces;
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
        var matches = await _matchRepository.GetAllAsync(new[] { "HomeTeam", "AwayTeam" });
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    public async Task<MatchDto?> GetByIdAsync(Guid id)
    {
        var match = await _matchRepository.GetByIdAsync(id, new[] { "HomeTeam", "AwayTeam", "Referee", "Events.Player" });
        if (match == null) return null;

        if (match.Events == null || !match.Events.Any())
        {
             var events = await _eventRepository.FindAsync(e => e.MatchId == id, new[] { "Player" });
             match.Events = events.ToList();
        }

        return _mapper.Map<MatchDto>(match);
    }

    public async Task<MatchDto> StartMatchAsync(Guid id)
    {
        var match = await _matchRepository.GetByIdAsync(id);
        if (match == null) throw new NotFoundException(nameof(Match), id);

        match.Status = MatchStatus.Live;
        await _matchRepository.UpdateAsync(match);
        await _analyticsService.LogActivityAsync("بدء مباراة", $"بدأت المباراة ذات المعرف {id}.", null, "Referee");
        
        // Lightweight System Event
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = MatchStatus.Live.ToString() }, $"match:{id}");
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = MatchStatus.Live.ToString() }, "role:Admin");
        
        // Notify Captains (requires fetching teams to get CaptainId)
        // Optimization: Fetch match with Teams included or fetch Teams separately.
        // Assuming match has HomeTeamId/AwayTeamId but usually not loaded.
        // Let's fetch teams.
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId);

        if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "بدأت المباراة", $"بدأت مباراتكم ضد {awayTeam?.Name ?? "الخصم"}.", "match");
        if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "بدأت المباراة", $"بدأت مباراتكم ضد {homeTeam?.Name ?? "الخصم"}.", "match");

        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }

    public async Task<MatchDto> EndMatchAsync(Guid id)
    {
        var match = await _matchRepository.GetByIdAsync(id);
        if (match == null) throw new NotFoundException(nameof(Match), id);

        match.Status = MatchStatus.Finished;
        await _matchRepository.UpdateAsync(match);
        await _analyticsService.LogActivityAsync("انتهاء مباراة", $"انتهت المباراة ذات المعرف {id}.", null, "Referee");

        // Lightweight System Event
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = MatchStatus.Finished.ToString() }, $"match:{id}");
        await _notifier.SendSystemEventAsync("MATCH_STATUS_CHANGED", new { MatchId = id, Status = MatchStatus.Finished.ToString() }, "role:Admin");

        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId);

        if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "انتهت المباراة", $"انتهت المباراة. النتيجة: {match.HomeScore}-{match.AwayScore}", "match");
        if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "انتهت المباراة", $"انتهت المباراة. النتيجة: {match.HomeScore}-{match.AwayScore}", "match");

        // Trigger Lifecycle check
        await _lifecycleService.CheckAndFinalizeTournamentAsync(match.TournamentId);

        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }

    public async Task<MatchDto> AddEventAsync(Guid id, AddMatchEventRequest request)
    {
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
            await _analyticsService.LogActivityAsync("تسجيل هدف", $"هدف لفريق {request.TeamId} في المباراة {id}", request.PlayerId, "Player");
        }

        await _eventRepository.AddAsync(matchEvent);
        
        // Refresh events for clean return
        var events = await _eventRepository.FindAsync(e => e.MatchId == id, new[] { "Player" });
        match.Events = events.ToList();

        // Notify
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId);
        if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "حدث جديد", "تم تحديث أحداث المباراة", "match");
        if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "حدث جديد", "تم تحديث أحداث المباراة", "match");
        if (match.RefereeId.HasValue && match.RefereeId.Value != Guid.Empty) await _notificationService.SendNotificationAsync(match.RefereeId.Value, "حدث جديد", "تم تحديث أحداث المباراة", "match");

        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }

    public async Task<MatchDto> RemoveEventAsync(Guid matchId, Guid eventId)
    {
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
        await _analyticsService.LogActivityAsync("إزالة حدث", $"تمت إزالة الحدث {eventId} من المباراة {matchId}", null, "Admin");

        // Notify
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId);
        if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "تحديث أحداث", "تم تحديث أحداث المباراة", "match");
        if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "تحديث أحداث", "تم تحديث أحداث المباراة", "match");
        if (match.RefereeId.HasValue && match.RefereeId.Value != Guid.Empty) await _notificationService.SendNotificationAsync(match.RefereeId.Value, "تحديث أحداث", "تم تحديث أحداث المباراة", "match");

        // Refresh events
        var events = await _eventRepository.FindAsync(e => e.MatchId == matchId, new[] { "Player" });
        match.Events = events.ToList();

        var matchDto = _mapper.Map<MatchDto>(match);
        await _notifier.SendMatchUpdatedAsync(matchDto);
        return matchDto;
    }

    public async Task<MatchDto> SubmitReportAsync(Guid id, SubmitReportRequest request)
    {
        var match = await _matchRepository.GetByIdAsync(id);
        if (match == null) throw new NotFoundException(nameof(Match), id);

        match.RefereeNotes = request.Notes;
        await _matchRepository.UpdateAsync(match);
        return _mapper.Map<MatchDto>(match);
    }

    public async Task<MatchDto> UpdateAsync(Guid id, UpdateMatchRequest request)
    {
        var match = await _matchRepository.GetByIdAsync(id, new[] { "HomeTeam", "AwayTeam", "Referee" });
        if (match == null) throw new NotFoundException(nameof(Match), id);

        var oldRefereeId = match.RefereeId;
        var oldStatus = match.Status;
        var oldScore = $"{match.HomeScore}-{match.AwayScore}";
        var oldDate = match.Date;

        bool scoreChanged = (request.HomeScore.HasValue && request.HomeScore != match.HomeScore) || 
                           (request.AwayScore.HasValue && request.AwayScore != match.AwayScore);
        
        if (request.HomeScore.HasValue) match.HomeScore = request.HomeScore.Value;
        if (request.AwayScore.HasValue) match.AwayScore = request.AwayScore.Value;
        if (request.Date.HasValue) match.Date = request.Date.Value;
        if (request.RefereeId.HasValue) match.RefereeId = request.RefereeId.Value;
        
        MatchStatus? newStatus = null;
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<MatchStatus>(request.Status, true, out var status))
        {
            newStatus = status;
            match.Status = status;
        }

        await _matchRepository.UpdateAsync(match);

        // Handle Notifications & Logging
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId);

        // 1. Score Update
        if (scoreChanged)
        {
            await _analyticsService.LogActivityAsync("تحديث النتيجة", $"تم تحديث نتيجة المباراة {id} إلى {match.HomeScore}-{match.AwayScore}", null, "AdminOverride");
            string msg = "تم تعديل نتيجة المباراة بواسطة الإدارة";
            if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "تعديل نتيجة", msg, "match");
            if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "تعديل نتيجة", msg, "match");
            if (match.RefereeId.HasValue && match.RefereeId.Value != Guid.Empty) await _notificationService.SendNotificationAsync(match.RefereeId.Value, "تعديل نتيجة", msg, "match");
        }

        // 2. Referee Change
        if (request.RefereeId.HasValue && request.RefereeId.Value != oldRefereeId)
        {
            if (oldRefereeId.HasValue && oldRefereeId.Value != Guid.Empty)
                await _notificationService.SendNotificationAsync(oldRefereeId.Value, "إلغاء تعيين", "تم إلغاء تعيينك من المباراة", "match");
            
            await _notificationService.SendNotificationAsync(request.RefereeId.Value, "تعيين جديد", "تم تعيينك حكماً للمباراة", "match");
            
            string msg = "تم تغيير حكم المباراة";
            if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "تغيير الحكم", msg, "match");
            if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "تغيير الحكم", msg, "match");
        }

        // 3. Status Changes (Postpone, Cancel, Reschedule)
        if (newStatus.HasValue && newStatus.Value != oldStatus)
        {
            string msg = "";
            switch (newStatus.Value)
            {
                case MatchStatus.Postponed:
                    msg = $"تم تأجيل المباراة إلى {match.Date:yyyy/MM/dd} الساعة {match.Date:HH:mm}";
                    break;
                case MatchStatus.Cancelled:
                    msg = "تم إلغاء المباراة رسمياً";
                    break;
                case MatchStatus.Rescheduled:
                    msg = $"سيتم إعادة المباراة يوم {match.Date:yyyy/MM/dd} الساعة {match.Date:HH:mm}";
                    break;
            }

            if (!string.IsNullOrEmpty(msg))
            {
                if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "تحديث حالة المباراة", msg, "match");
                if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "تحديث حالة المباراة", msg, "match");
                if (match.RefereeId.HasValue && match.RefereeId.Value != Guid.Empty) await _notificationService.SendNotificationAsync(match.RefereeId.Value, "تحديث حالة المباراة", msg, "match");
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

        // Reload to get fresh data incl. new referee name
        match = await _matchRepository.GetByIdAsync(id, new[] { "HomeTeam", "AwayTeam", "Referee" });
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

        await _analyticsService.LogActivityAsync("توليد مباريات", $"تم توليد {matches.Count} مباراة للبطولة {tournamentId}", null, "System");

        var matchDtos = _mapper.Map<IEnumerable<MatchDto>>(matches);
        await _notifier.SendMatchesGeneratedAsync(matchDtos);

        return matchDtos;
    }

    public async Task<IEnumerable<MatchDto>> GetMatchesByRefereeAsync(Guid refereeId)
    {
        var matches = await _matchRepository.FindAsync(m => m.RefereeId == refereeId, new[] { "HomeTeam", "AwayTeam" });
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }
}
