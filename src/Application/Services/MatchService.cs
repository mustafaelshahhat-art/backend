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
        var matches = await _matchRepository.GetAllAsync();
        return _mapper.Map<IEnumerable<MatchDto>>(matches);
    }

    public async Task<MatchDto?> GetByIdAsync(Guid id)
    {
        var match = await _matchRepository.GetByIdAsync(id);
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
        var match = await _matchRepository.GetByIdAsync(id);
        if (match == null) throw new NotFoundException(nameof(Match), id);

        if (request.HomeScore.HasValue) match.HomeScore = request.HomeScore.Value;
        if (request.AwayScore.HasValue) match.AwayScore = request.AwayScore.Value;
        if (request.Date.HasValue) match.Date = request.Date.Value;
        
        if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<MatchStatus>(request.Status, true, out var status))
        {
            match.Status = status;
        }

        await _matchRepository.UpdateAsync(match);
        return _mapper.Map<MatchDto>(match);
    }
}
