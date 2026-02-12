using System;
using System.Collections.Generic;
using System.Linq;
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

    public async Task<AnalyticsOverview> GetOverviewAsync(Guid? creatorId = null)
    {
        int totalUsers = 0, totalTeams = 0, activeTournaments = 0, matchesToday = 0, loginsToday = 0, totalGoals = 0;
        var today = DateTime.UtcNow.Date;

        if (creatorId.HasValue)
        {
            // Scoped counts for creators
            activeTournaments = await _tournamentRepository.CountAsync(t => 
                t.CreatorUserId == creatorId.Value && 
                (t.Status == "registration_open" || t.Status == "active"));
            
            // Count distinct teams across all tournaments of this creator
            var registrations = await _registrationRepository.FindAsync(r => r.Tournament.CreatorUserId == creatorId.Value);
            totalTeams = registrations.Select(r => r.TeamId).Distinct().Count(); // Still a bit in-memory but better than downloading tournaments
            
            matchesToday = await _matchRepository.CountAsync(m => 
                m.Date.HasValue && m.Date.Value.Date == today && 
                m.Tournament.CreatorUserId == creatorId.Value);
            
            var homeGoals = await _matchRepository.SumAsync(m => 
                m.Status == Domain.Enums.MatchStatus.Finished && m.Tournament.CreatorUserId == creatorId.Value, 
                m => m.HomeScore);
            var awayGoals = await _matchRepository.SumAsync(m => 
                m.Status == Domain.Enums.MatchStatus.Finished && m.Tournament.CreatorUserId == creatorId.Value, 
                m => m.AwayScore);
            totalGoals = (int)(homeGoals + awayGoals);
        }
        else
        {
            // Global counts for admins
            totalUsers = await _userRepository.CountAsync(u => u.IsEmailVerified);
            totalTeams = await _teamRepository.CountAsync(_ => true);
            activeTournaments = await _tournamentRepository.CountAsync(t => t.Status == "registration_open" || t.Status == "active");
            matchesToday = await _matchRepository.CountAsync(m => m.Date.HasValue && m.Date.Value.Date == today);
            loginsToday = await _activityRepository.CountAsync(a => a.Type == Common.ActivityConstants.USER_LOGIN && a.CreatedAt.Date == today);
            
            var homeGoals = await _matchRepository.SumAsync(m => m.Status == Domain.Enums.MatchStatus.Finished, m => m.HomeScore);
            var awayGoals = await _matchRepository.SumAsync(m => m.Status == Domain.Enums.MatchStatus.Finished, m => m.AwayScore);
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

    public async Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId)
    {
        var playerCountTask = _playerRepository.CountAsync(p => p.TeamId == teamId);
        var matchCountTask = _matchRepository.CountAsync(m => 
            (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && 
            m.Status == Domain.Enums.MatchStatus.Scheduled);
        
        var tournamentCountTask = _registrationRepository.CountAsync(r => 
            r.TeamId == teamId && 
            r.Status == Domain.Enums.RegistrationStatus.Approved &&
            (r.Tournament.Status == "active" || r.Tournament.Status == "registration_open"));

        await Task.WhenAll(playerCountTask, matchCountTask, tournamentCountTask);

        return new TeamAnalyticsDto
        {
            PlayerCount = await playerCountTask,
            UpcomingMatches = await matchCountTask,
            ActiveTournaments = await tournamentCountTask,
            Rank = "N/A"
        };
    }

    public async Task<IEnumerable<ActivityDto>> GetRecentActivitiesAsync(Guid? creatorId = null)
    {
        IEnumerable<Activity> activities;
        
        if (creatorId.HasValue)
        {
            activities = await _activityRepository.GetNoTrackingAsync(a => a.UserId == creatorId.Value);
        }
        else
        {
            // Limit to top 100 to avoid performance hit on large logs
            var allActivities = await _activityRepository.GetAllNoTrackingAsync(Array.Empty<string>());
            activities = allActivities.OrderByDescending(a => a.CreatedAt).Take(100);
        }
        
        var sorted = activities.OrderByDescending(a => a.CreatedAt).Take(20);
        
        return sorted.Select(a => 
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

    public async Task LogActivityAsync(string type, string message, Guid? userId = null, string? userName = null)
    {
        var activity = new Activity
        {
            Type = type,
            Message = message,
            UserId = userId,
            UserName = userName
        };
        await _activityRepository.AddAsync(activity);
    }

    public async Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null)
    {
        var localized = Application.Common.ActivityConstants.GetLocalized(code, placeholders);
        
        // We store "Category" in Type (for Badge/Filter) 
        // We store "Title: Message" in Message (for readability)
        // Or better: Just message, and let Frontend map Code if we stored Code.
        // But user asked to keep compatibility. 
        // Previously Type was "بدء مباراة" (Title).
        // Let's stick to storing the Internal Code in Type as requested by "Activity Type Normalization" rule 1.
        // Frontend will be updated to map Code -> Category.
        
        var activity = new Activity
        {
            Type = code, 
            Message = localized.Message,
            UserId = userId,
            UserName = userName
        };
        await _activityRepository.AddAsync(activity);
    }
}

