using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Analytics;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IRepository<Activity> _activityRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<Player> _playerRepository;
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<TeamRegistration> _registrationRepository;
    private readonly IMapper _mapper;
    private readonly IBackgroundActivityLogger _backgroundLogger;

    public AnalyticsService(
        IRepository<Activity> activityRepository,
        IRepository<User> userRepository,
        IRepository<Team> teamRepository,
        IRepository<Player> playerRepository,
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IRepository<TeamRegistration> registrationRepository,
        IMapper mapper,
        IBackgroundActivityLogger backgroundLogger)
    {
        _activityRepository = activityRepository;
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _registrationRepository = registrationRepository;
        _mapper = mapper;
        _backgroundLogger = backgroundLogger;
    }

    public async Task<AnalyticsOverview> GetOverviewAsync(Guid? creatorId = null, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        if (creatorId.HasValue)
        {
            // PROD-AUDIT: Consolidated query for creator stats
            var tournamentStats = await _tournamentRepository.GetQueryable()
                .Where(t => t.CreatorUserId == creatorId.Value)
                .Select(t => new { 
                    IsActive = t.Status == Domain.Enums.TournamentStatus.RegistrationOpen || t.Status == Domain.Enums.TournamentStatus.Active,
                    TeamCount = t.Registrations.Count(r => r.Status == Domain.Enums.RegistrationStatus.Approved),
                    MatchCountToday = t.Matches.Count(m => m.Date.HasValue && m.Date.Value.Date == today),
                    Goals = t.Matches.Where(m => m.Status == Domain.Enums.MatchStatus.Finished).Sum(m => (int?)m.HomeScore + (int?)m.AwayScore) ?? 0
                })
                .ToListAsync(ct);

            return new AnalyticsOverview
            {
                ActiveTournaments = tournamentStats.Count(x => x.IsActive),
                TotalTeams = tournamentStats.Sum(x => x.TeamCount),
                MatchesToday = tournamentStats.Sum(x => x.MatchCountToday),
                TotalGoals = tournamentStats.Sum(x => x.Goals),
                TotalUsers = 0, // Creators don't see global user count
                LoginsToday = 0
            };
        }
        else
        {
            // Global counts for admins - Sequential to match EF Core threading model per Context instance
            var activeTournaments = await _tournamentRepository.CountAsync(t => t.Status == Domain.Enums.TournamentStatus.RegistrationOpen || t.Status == Domain.Enums.TournamentStatus.Active, ct);
            var users = await _userRepository.CountAsync(u => u.IsEmailVerified, ct);
            var teams = await _teamRepository.CountAsync(_ => true, ct);
            
            var matchesToday = await _matchRepository.CountAsync(m => m.Date.HasValue && m.Date.Value.Date == today, ct);
            var loginsToday = await _activityRepository.CountAsync(a => 
                (a.Type == Common.ActivityConstants.USER_LOGIN || a.Type == Common.ActivityConstants.GUEST_VISIT) 
                && a.CreatedAt.Date == today, ct);
            
            var totalGoals = await _matchRepository.GetQueryable()
                .Where(m => m.Status == Domain.Enums.MatchStatus.Finished)
                .SumAsync(m => (int?)m.HomeScore + (int?)m.AwayScore, ct) ?? 0;

            return new AnalyticsOverview
            {
                TotalUsers = users,
                TotalTeams = teams,
                ActiveTournaments = activeTournaments,
                MatchesToday = matchesToday,
                LoginsToday = loginsToday,
                TotalGoals = totalGoals
            };
        }
    }

    public async Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId, CancellationToken ct = default)
    {
        var playerCount = await _playerRepository.CountAsync(p => p.TeamId == teamId, ct);
        var matchCount = await _matchRepository.CountAsync(m => 
            (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && 
            m.Status == Domain.Enums.MatchStatus.Scheduled, ct);
        
        var tournamentCount = await _registrationRepository.CountAsync(r => 
            r.TeamId == teamId && 
            r.Status == Domain.Enums.RegistrationStatus.Approved &&
            (r.Tournament != null && (r.Tournament.Status == Domain.Enums.TournamentStatus.Active || r.Tournament.Status == Domain.Enums.TournamentStatus.RegistrationOpen)), ct);

        return new TeamAnalyticsDto
        {
            PlayerCount = playerCount,
            UpcomingMatches = matchCount,
            ActiveTournaments = tournamentCount,
            Rank = "N/A"
        };
    }

    public async Task<Application.Common.Models.PagedResult<ActivityDto>> GetRecentActivitiesAsync(
        ActivityFilterParams filters, Guid? creatorId = null, CancellationToken ct = default)
    {
        var page = filters.Page < 1 ? 1 : filters.Page;
        var pageSize = filters.PageSize > 100 ? 100 : (filters.PageSize < 1 ? 20 : filters.PageSize);

        var query = _activityRepository.GetQueryable();

        // ── Apply filters ──
        if (creatorId.HasValue)
            query = query.Where(a => a.UserId == creatorId.Value);

        if (filters.UserId.HasValue)
            query = query.Where(a => a.UserId == filters.UserId.Value);

        if (!string.IsNullOrEmpty(filters.ActorRole))
            query = query.Where(a => a.ActorRole == filters.ActorRole);

        if (!string.IsNullOrEmpty(filters.ActionType))
            query = query.Where(a => a.Type == filters.ActionType);

        if (!string.IsNullOrEmpty(filters.EntityType))
            query = query.Where(a => a.EntityType == filters.EntityType);

        if (filters.MinSeverity.HasValue)
            query = query.Where(a => (int)a.Severity >= filters.MinSeverity.Value);

        if (filters.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= filters.FromDate.Value);

        if (filters.ToDate.HasValue)
            query = query.Where(a => a.CreatedAt <= filters.ToDate.Value);

        // ── Count + Page (DTO Projection — no entity loading) ──
        var totalCount = await query.CountAsync(ct);

        var dtos = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ActivityDto
            {
                Id = a.Id,
                ActionType = a.Type,
                Message = a.Message,
                Timestamp = a.CreatedAt,
                Time = "",
                UserName = a.UserName,
                Severity = a.Severity == Domain.Enums.ActivitySeverity.Critical ? "critical"
                         : a.Severity == Domain.Enums.ActivitySeverity.Warning ? "warning" : "info",
                ActorRole = a.ActorRole,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                EntityName = a.EntityName
            })
            .ToListAsync(ct);

        // Enrich Arabic category from constants (in-memory — O(n) dictionary lookups)
        foreach (var dto in dtos)
        {
            dto.Type = Common.ActivityConstants.Library.TryGetValue(dto.ActionType, out var meta)
                ? meta.CategoryAr
                : "نظام";
        }

        return new Application.Common.Models.PagedResult<ActivityDto>(dtos, totalCount, page, pageSize);
    }

    // ── Logging (backward compat) ──

    public Task LogActivityAsync(string type, string message, Guid? userId = null, string? userName = null, CancellationToken ct = default)
    {
        _backgroundLogger.LogActivity(type, message, userId, userName);
        return Task.CompletedTask;
    }

    public Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null, CancellationToken ct = default)
    {
        _backgroundLogger.LogActivityByTemplate(code, placeholders, userId, userName);
        return Task.CompletedTask;
    }

    // ── Enriched logging ──

    public Task LogActivityAsync(string type, string message, Guid? userId, string? userName,
        string? actorRole, Guid? entityId, string? entityType, string? entityName, string? metadata, CancellationToken ct = default)
    {
        _backgroundLogger.LogActivity(type, message, userId, userName, actorRole, entityId, entityType, entityName, metadata);
        return Task.CompletedTask;
    }

    public Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId, string? userName,
        string? actorRole, Guid? entityId, string? entityType, string? entityName, string? metadata, CancellationToken ct = default)
    {
        _backgroundLogger.LogActivityByTemplate(code, placeholders, userId, userName, actorRole, entityId, entityType, entityName, metadata);
        return Task.CompletedTask;
    }
}
