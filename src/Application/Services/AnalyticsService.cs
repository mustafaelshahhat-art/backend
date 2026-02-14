using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Analytics;
using Application.DTOs.Notifications;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Interfaces;

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

    public AnalyticsService(
        IRepository<Activity> activityRepository,
        IRepository<User> userRepository,
        IRepository<Team> teamRepository,
        IRepository<Player> playerRepository,
        IRepository<Tournament> tournamentRepository,
        IRepository<Match> matchRepository,
        IRepository<TeamRegistration> registrationRepository,
        IMapper mapper)
    {
        _activityRepository = activityRepository;
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _tournamentRepository = tournamentRepository;
        _matchRepository = matchRepository;
        _registrationRepository = registrationRepository;
        _mapper = mapper;
    }

    public async Task<AnalyticsOverview> GetOverviewAsync(Guid? creatorId = null, CancellationToken ct = default)
    {
        int totalUsers = 0, totalTeams = 0, activeTournaments = 0, matchesToday = 0, loginsToday = 0, totalGoals = 0;
        var today = DateTime.UtcNow.Date;

        if (creatorId.HasValue)
        {
            // Scoped counts for creators
            activeTournaments = await _tournamentRepository.CountAsync(t => 
                t.CreatorUserId == creatorId.Value && 
                (t.Status == Domain.Enums.TournamentStatus.RegistrationOpen || t.Status == Domain.Enums.TournamentStatus.Active), ct);
            
            // Count distinct teams across all tournaments of this creator
            var registrations = await _registrationRepository.FindAsync(r => r.Tournament != null && r.Tournament.CreatorUserId == creatorId.Value, ct);
            totalTeams = registrations.Select(r => r.TeamId).Distinct().Count(); // Still a bit in-memory but better than downloading tournaments
            
            matchesToday = await _matchRepository.CountAsync(m => 
                m.Date.HasValue && m.Date.Value.Date == today && 
                m.Tournament != null && m.Tournament.CreatorUserId == creatorId.Value, ct);
            
            var homeGoals = await _matchRepository.SumAsync(m => 
                m.Status == Domain.Enums.MatchStatus.Finished && m.Tournament != null && m.Tournament.CreatorUserId == creatorId.Value, 
                m => m.HomeScore, ct);
            var awayGoals = await _matchRepository.SumAsync(m => 
                m.Status == Domain.Enums.MatchStatus.Finished && m.Tournament != null && m.Tournament.CreatorUserId == creatorId.Value, 
                m => m.AwayScore, ct);
            totalGoals = (int)(homeGoals + awayGoals);
        }
        else
        {
            // Global counts for admins
            totalUsers = await _userRepository.CountAsync(u => u.IsEmailVerified, ct);
            totalTeams = await _teamRepository.CountAsync(_ => true, ct);
            activeTournaments = await _tournamentRepository.CountAsync(t => t.Status == Domain.Enums.TournamentStatus.RegistrationOpen || t.Status == Domain.Enums.TournamentStatus.Active, ct);
            matchesToday = await _matchRepository.CountAsync(m => m.Date.HasValue && m.Date.Value.Date == today, ct);
            loginsToday = await _activityRepository.CountAsync(a => a.Type == Common.ActivityConstants.USER_LOGIN && a.CreatedAt.Date == today, ct);
            
            var homeGoals = await _matchRepository.SumAsync(m => m.Status == Domain.Enums.MatchStatus.Finished, m => m.HomeScore, ct);
            var awayGoals = await _matchRepository.SumAsync(m => m.Status == Domain.Enums.MatchStatus.Finished, m => m.AwayScore, ct);
            totalGoals = (int)(homeGoals + awayGoals);
        }

        return new AnalyticsOverview
        {
            TotalUsers = totalUsers,
            TotalTeams = totalTeams,
            ActiveTournaments = activeTournaments,
            MatchesToday = matchesToday,
            LoginsToday = loginsToday,
            TotalGoals = totalGoals
        };
    }

    public async Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId, CancellationToken ct = default)
    {
        var playerCountTask = _playerRepository.CountAsync(p => p.TeamId == teamId, ct);
        var matchCountTask = _matchRepository.CountAsync(m => 
            (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && 
            m.Status == Domain.Enums.MatchStatus.Scheduled, ct);
        
        var tournamentCountTask = _registrationRepository.CountAsync(r => 
            r.TeamId == teamId && 
            r.Status == Domain.Enums.RegistrationStatus.Approved &&
            (r.Tournament != null && (r.Tournament.Status == Domain.Enums.TournamentStatus.Active || r.Tournament.Status == Domain.Enums.TournamentStatus.RegistrationOpen)), ct);

        await Task.WhenAll(playerCountTask, matchCountTask, tournamentCountTask);

        return new TeamAnalyticsDto
        {
            PlayerCount = await playerCountTask,
            UpcomingMatches = await matchCountTask,
            ActiveTournaments = await tournamentCountTask,
            Rank = "N/A"
        };
    }

    public async Task<IEnumerable<ActivityDto>> GetRecentActivitiesAsync(Guid? creatorId = null, CancellationToken ct = default)
    {
        IEnumerable<Activity> activities;
        
        if (creatorId.HasValue)
        {
            var result = await _activityRepository.GetPagedAsync(1, 20, a => a.UserId == creatorId.Value, q => q.OrderByDescending(a => a.CreatedAt), ct);
            activities = result.Items;
        }
        else
        {
            var result = await _activityRepository.GetPagedAsync(1, 20, null, q => q.OrderByDescending(a => a.CreatedAt), ct);
            activities = result.Items;
        }
        
        return activities.Select(a => 
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
        });
    }

    public async Task LogActivityAsync(string type, string message, Guid? userId = null, string? userName = null, CancellationToken ct = default)
    {
        var activity = new Activity
        {
            Type = type,
            Message = message,
            UserId = userId,
            UserName = userName
        };
        await _activityRepository.AddAsync(activity, ct);
    }

    public async Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null, CancellationToken ct = default)
    {
        var localized = Application.Common.ActivityConstants.GetLocalized(code, placeholders);
        
        var activity = new Activity
        {
            Type = code, 
            Message = localized.Message,
            UserId = userId,
            UserName = userName
        };
        await _activityRepository.AddAsync(activity, ct);
    }
}


