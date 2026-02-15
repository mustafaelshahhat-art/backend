using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Analytics;
using Application.DTOs.Notifications;
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
            var loginsToday = await _activityRepository.CountAsync(a => a.Type == Common.ActivityConstants.USER_LOGIN && a.CreatedAt.Date == today, ct);
            
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

    public async Task<Application.Common.Models.PagedResult<ActivityDto>> GetRecentActivitiesAsync(int page, int pageSize, Guid? creatorId = null, CancellationToken ct = default)
    {
        if (pageSize > 100) pageSize = 100;

        Expression<Func<Activity, bool>>? predicate = creatorId.HasValue ? a => a.UserId == creatorId.Value : null;

        var (items, totalCount) = await _activityRepository.GetPagedAsync(
            page,
            pageSize,
            predicate,
            q => q.OrderByDescending(a => a.CreatedAt),
            ct
        );
        
        var dtos = items.Select(a => 
        {
            var localized = Common.ActivityConstants.GetLocalized(a.Type, null);
            return new ActivityDto
            {
                Type = localized.Category,
                Message = a.Message,
                Timestamp = a.CreatedAt,
                Time = "", 
                UserName = a.UserName
            };
        }).ToList();

        return new Application.Common.Models.PagedResult<ActivityDto>(dtos, totalCount, page, pageSize);
    }

    public Task LogActivityAsync(string type, string message, Guid? userId = null, string? userName = null, CancellationToken ct = default) // Modified method signature
    {
        _backgroundLogger.LogActivity(type, message, userId, userName); // Modified implementation
        return Task.CompletedTask; // Modified implementation
    }

    public Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null, CancellationToken ct = default) // Modified method signature
    {
        _backgroundLogger.LogActivityByTemplate(code, placeholders, userId, userName); // Modified implementation
        return Task.CompletedTask; // Modified implementation
    }
}
