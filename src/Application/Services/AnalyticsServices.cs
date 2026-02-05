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
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly IRepository<Objection> _objectionRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IMapper _mapper;

    public AnalyticsService(
        IRepository<Activity> activityRepository,
        IRepository<User> userRepository,
        IRepository<Team> teamRepository,
        IRepository<Tournament> tournamentRepository,
        IRepository<Objection> objectionRepository,
        IRepository<Match> matchRepository,
        IMapper mapper)
    {
        _activityRepository = activityRepository;
        _userRepository = userRepository;
        _teamRepository = teamRepository;
        _tournamentRepository = tournamentRepository;
        _objectionRepository = objectionRepository;
        _matchRepository = matchRepository;
        _mapper = mapper;
    }

    public async Task<AnalyticsOverview> GetOverviewAsync()
    {
        var users = await _userRepository.GetAllAsync();
        var teams = await _teamRepository.GetAllAsync();
        var tournaments = await _tournamentRepository.GetAllAsync();
        var objections = await _objectionRepository.FindAsync(o => o.Status == Domain.Enums.ObjectionStatus.Pending);

        var today = DateTime.UtcNow.Date;
        var matches = await _matchRepository.FindAsync(m => m.Date.HasValue && m.Date.Value.Date == today);

        return new AnalyticsOverview
        {
            TotalUsers = users.Count(),
            TotalTeams = teams.Count(),
            ActiveTournaments = tournaments.Count(t => t.Status == "registration_open" || t.Status == "active"),
            PendingObjections = objections.Count(),
            MatchesToday = matches.Count()
        };
    }

    public async Task<IEnumerable<ActivityDto>> GetRecentActivitiesAsync()
    {
        var activities = await _activityRepository.GetAllAsync();
        // Sort DESC
        var sorted = activities.OrderByDescending(a => a.CreatedAt).Take(20);
        
        var dtos = new List<ActivityDto>();
        foreach (var a in sorted)
        {
            dtos.Add(new ActivityDto
            {
                Type = a.Type,
                Message = a.Message,
                Timestamp = a.CreatedAt,
                Time = FormatTime(a.CreatedAt),
                UserName = a.UserName
            });
        }
        return dtos;
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
    
    private string FormatTime(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} mins ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} hours ago";
        return $"{(int)diff.TotalDays} days ago";
    }
}

