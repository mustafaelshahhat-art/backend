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
    private readonly IRepository<Objection> _objectionRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;

    public AnalyticsService(
        IRepository<Activity> activityRepository,
        IRepository<User> userRepository,
        IRepository<Team> teamRepository,
        IRepository<Player> playerRepository,
        IRepository<Tournament> tournamentRepository,
        IRepository<Objection> objectionRepository,
        IRepository<Match> matchRepository,
        IMapper mapper)
    {
        _activityRepository = activityRepository;
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _playerRepository = playerRepository;
        _tournamentRepository = tournamentRepository;
        _objectionRepository = objectionRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
    }

    public async Task<AnalyticsOverview> GetOverviewAsync()
    {
        var totalUsers = await _userRepository.CountAsync(_ => true);
        var totalTeams = await _teamRepository.CountAsync(_ => true);
        var activeTournaments = await _tournamentRepository.CountAsync(t => t.Status == "registration_open" || t.Status == "active");
        var pendingObjections = await _objectionRepository.CountAsync(o => o.Status == Domain.Enums.ObjectionStatus.Pending);

        var today = DateTime.UtcNow.Date;
        var matchesToday = await _matchRepository.CountAsync(m => m.Date.HasValue && m.Date.Value.Date == today);
        
        var finishedMatches = await _matchRepository.FindAsync(m => m.Status == Domain.Enums.MatchStatus.Finished);
        var totalGoals = finishedMatches.Sum(m => m.HomeScore + m.AwayScore);

        return new AnalyticsOverview
        {
            TotalUsers = totalUsers,
            TotalTeams = totalTeams,
            ActiveTournaments = activeTournaments,
            PendingObjections = pendingObjections,
            MatchesToday = matchesToday,
            TotalGoals = totalGoals
        };
    }

    public async Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId)
    {
        var players = await _playerRepository.FindAsync(p => p.TeamId == teamId);
        var upcomingMatches = await _matchRepository.FindAsync(m => 
            (m.HomeTeamId == teamId || m.AwayTeamId == teamId) && 
            m.Status == Domain.Enums.MatchStatus.Scheduled);
        
        var registrations = await _tournamentRepository.GetAllAsync(t => t.Registrations); // N+1ish but ok
        // Find tournaments where this team is registered and status is active/open
        var activeTournaments = registrations.Count(t => 
            (t.Status == "active" || t.Status == "registration_open") &&
            t.Registrations.Any(r => r.TeamId == teamId && r.Status == Domain.Enums.RegistrationStatus.Approved));

        return new TeamAnalyticsDto
        {
            PlayerCount = players.Count(),
            UpcomingMatches = upcomingMatches.Count(),
            ActiveTournaments = activeTournaments,
            Rank = "3" // Placeholder or implement standing calculation
        };
    }

    public async Task<IEnumerable<ActivityDto>> GetRecentActivitiesAsync()
    {
        var activities = await _activityRepository.GetAllAsync();
        // Sort DESC
        var sorted = activities.OrderByDescending(a => a.CreatedAt).Take(20);
        
        return sorted.Select(a => new ActivityDto
        {
            Type = a.Type,
            Message = a.Message,
            Timestamp = a.CreatedAt,
            Time = "", // Deprecated, frontend will use Timestamp with pipe
            UserName = a.UserName
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
}

