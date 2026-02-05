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

    public MatchService(
        IRepository<Match> matchRepository,
        IRepository<MatchEvent> eventRepository,
        IRepository<Team> teamRepository,
        IMapper mapper,
        IAnalyticsService analyticsService,
        INotificationService notificationService)
    {
        _matchRepository = matchRepository;
        _eventRepository = eventRepository;
        _teamRepository = teamRepository;
        _mapper = mapper;
        _analyticsService = analyticsService;
        _notificationService = notificationService;
    }

    public async Task<IEnumerable<MatchDto>> GetAllAsync()
    {
        var matches = await _matchRepository.GetAllAsync(new[] { "HomeTeam", "AwayTeam" });
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    public async Task<MatchDto?> GetByIdAsync(Guid id)
    {
        var match = await _matchRepository.GetByIdAsync(id, new[] { "HomeTeam", "AwayTeam" });
        if (match == null) return null;

        if (match.Events == null || !match.Events.Any())
        {
             var events = await _eventRepository.FindAsync(e => e.MatchId == id);
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
        await _analyticsService.LogActivityAsync("Match Started", $"Match ID {id} started.", null, "Referee");
        
        // Notify Captains (requires fetching teams to get CaptainId)
        // Optimization: Fetch match with Teams included or fetch Teams separately.
        // Assuming match has HomeTeamId/AwayTeamId but usually not loaded.
        // Let's fetch teams.
        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId);

        if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "Match Started", $"Your match against {awayTeam?.Name ?? "Opponent"} has started.", "match");
        if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "Match Started", $"Your match against {homeTeam?.Name ?? "Opponent"} has started.", "match");

        return _mapper.Map<MatchDto>(match);
    }

    public async Task<MatchDto> EndMatchAsync(Guid id)
    {
        var match = await _matchRepository.GetByIdAsync(id);
        if (match == null) throw new NotFoundException(nameof(Match), id);

        match.Status = MatchStatus.Finished;
        await _matchRepository.UpdateAsync(match);
        await _analyticsService.LogActivityAsync("Match Ended", $"Match ID {id} ended.", null, "Referee");

        var homeTeam = await _teamRepository.GetByIdAsync(match.HomeTeamId);
        var awayTeam = await _teamRepository.GetByIdAsync(match.AwayTeamId);

        if (homeTeam != null) await _notificationService.SendNotificationAsync(homeTeam.CaptainId, "Match Ended", $"Match ended. Score: {match.HomeScore}-{match.AwayScore}", "match");
        if (awayTeam != null) await _notificationService.SendNotificationAsync(awayTeam.CaptainId, "Match Ended", $"Match ended. Score: {match.HomeScore}-{match.AwayScore}", "match");

        return _mapper.Map<MatchDto>(match);
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
            await _analyticsService.LogActivityAsync("Goal Scored", $"Goal for Team {request.TeamId} in Match {id}", request.PlayerId, "Player");
        }

        await _eventRepository.AddAsync(matchEvent);
        
        // Refresh events for clean return
        var events = await _eventRepository.FindAsync(e => e.MatchId == id);
        match.Events = events.ToList();

        return _mapper.Map<MatchDto>(match);
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

        if (request.HomeScore.HasValue) match.HomeScore = request.HomeScore.Value;
        if (request.AwayScore.HasValue) match.AwayScore = request.AwayScore.Value;
        if (request.Date.HasValue) match.Date = request.Date.Value;
        if (request.RefereeId.HasValue) match.RefereeId = request.RefereeId.Value;
        
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<MatchStatus>(request.Status, true, out var status))
        {
            match.Status = status;
        }

        await _matchRepository.UpdateAsync(match);
        
        // If Referee was updated, we might need to reload to get Referee name if EF didn't fix it up locally (it usually requires a fresh fetch or attached entry)
        // But for now, let's assume if we just set RefereeId, the Referee navigation prop might be null if not loaded. 
        // We included "Referee" in GetByIdAsync, but that was the OLD referee.
        // If we changed it, match.Referee will act weird unless we reload or the repo handles it.
        // Let's reload just to be safe if RefereeId changed.
        if (request.RefereeId.HasValue)
        {
             match = await _matchRepository.GetByIdAsync(id, new[] { "HomeTeam", "AwayTeam", "Referee" });
        }

        return _mapper.Map<MatchDto>(match);
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

        await _analyticsService.LogActivityAsync("Matches Generated", $"Generated {matches.Count} matches for Tournament {tournamentId}", null, "System");

        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    public async Task<IEnumerable<MatchDto>> GetMatchesByRefereeAsync(Guid refereeId)
    {
        var matches = await _matchRepository.FindAsync(m => m.RefereeId == refereeId, new[] { "HomeTeam", "AwayTeam" });
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }
}
