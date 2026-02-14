using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class ActivityLogMigrationService
{
    private readonly IRepository<Activity> _activityRepository;
    private readonly IRepository<Team> _teamRepository;
    private readonly IRepository<Match> _matchRepository;
    private readonly IRepository<Tournament> _tournamentRepository;
    private readonly ILogger<ActivityLogMigrationService> _logger;

    public ActivityLogMigrationService(
        IRepository<Activity> activityRepository,
        IRepository<Team> teamRepository,
        IRepository<Match> matchRepository,
        IRepository<Tournament> tournamentRepository,
        ILogger<ActivityLogMigrationService> logger)
    {
        _activityRepository = activityRepository;
        _teamRepository = teamRepository;
        _matchRepository = matchRepository;
        _tournamentRepository = tournamentRepository;
        _logger = logger;
    }

    public async Task MigrateLegacyLogsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Activity Log Migration...");

        var activities = (await _activityRepository.GetAllAsync(ct)).ToList();
        bool anyUpdated = false;

        // Cache lookups for performance (assuming reasonable dataset size)
        var teams = (await _teamRepository.GetAllAsync(ct)).ToDictionary(t => t.Id, t => t.Name);
        var tournaments = (await _tournamentRepository.GetAllAsync(ct)).ToDictionary(t => t.Id, t => t.Name);
        var matchesInfo = (await _matchRepository.GetAllAsync(new[] { "HomeTeam", "AwayTeam" }, ct))
            .ToDictionary(m => m.Id, m => $"{m.HomeTeam?.Name ?? "فريق"} vs {m.AwayTeam?.Name ?? "فريق"}");

        foreach (var activity in activities)
        {
            bool updated = false;

            // 1. Actor Normalization
            if (string.IsNullOrEmpty(activity.UserName) || activity.UserName == "System")
            {
                activity.UserName = "النظام";
                updated = true;
            }
            else if (activity.UserName == "AdminOverride")
            {
                activity.UserName = "الإدارة";
                updated = true;
            }
            else if (activity.UserName == "Admin")
            {
                activity.UserName = "مشرف النظام";
                updated = true;
            }
            else if (activity.UserName == "Player")
            {
                activity.UserName = "لاعب";
                updated = true;
            }


            // 2. Type/Category Normalization
            string originalType = activity.Type ?? "";
            
            // Map Legacy Type/Message to New Constants
            if (originalType == "User Registered" || originalType == "User Created")
            {
                activity.Type = ActivityConstants.USER_REGISTERED;
                updated = true;
            }
            else if (originalType == "User Login" || activity.Message.Contains("logged in"))
            {
                activity.Type = ActivityConstants.USER_LOGIN;
                updated = true;
            }
            else if (originalType == "Match Started")
            {
                activity.Type = ActivityConstants.MATCH_STARTED;
                updated = true;
            }
            else if (originalType == "Match Ended")
            {
                activity.Type = ActivityConstants.MATCH_ENDED;
                updated = true;
            }
            else if (originalType == "Match Score Updated" || originalType == "تحديث النتيجة")
            {
                activity.Type = ActivityConstants.MATCH_SCORE_UPDATED;
                updated = true;
            }
            else if (originalType == "Match Event Added" || originalType == "تسجيل هدف")
            {
                activity.Type = ActivityConstants.MATCH_EVENT_ADDED;
                updated = true;
            }
            else if (originalType == "Tournament Created" || originalType == "إنشاء بطولة")
            {
                activity.Type = ActivityConstants.TOURNAMENT_CREATED;
                updated = true;
            }
            else if (originalType == "Team Created" || originalType == "إنشاء فريق")
            {
                activity.Type = ActivityConstants.TEAM_CREATED;
                updated = true;
            }
            
            // If Type is generic english, map to Arabic Category
            if (activity.Type == null || !ActivityConstants.Library.ContainsKey(activity.Type))
            {
                // Fallback Mapping
                if (originalType.Contains("Match") || originalType.Contains("مباراة")) activity.Type = "مباراة";
                else if (originalType.Contains("User") || originalType.Contains("مستخدم")) activity.Type = "مستخدم";
                else if (originalType.Contains("Team") || originalType.Contains("فريق")) activity.Type = "فريق";
                else if (originalType.Contains("Tournament") || originalType.Contains("بطولة")) activity.Type = "بطولة";
                else if (originalType.Contains("Admin") || originalType.Contains("إدارة")) activity.Type = "إدارة";
                
                if (activity.Type != originalType) updated = true;
            }

            // 3. Message Localization & GUID Sanitization
            string msg = activity.Message;

            // Specific Replacements
            if (msg.Contains("registered."))
            {
                var match = System.Text.RegularExpressions.Regex.Match(msg, @"User (.*?) registered\.");
                if (match.Success)
                {
                    msg = $"تم تسجيل مستخدم جديد باسم {match.Groups[1].Value}";
                }
                else
                {
                    msg = "تم تسجيل مستخدم جديد";
                }
            }
            else if (msg.Contains("logged in"))
            {
                 var match = System.Text.RegularExpressions.Regex.Match(msg, @"User (.*?) logged in\.");
                 if (match.Success)
                 {
                     msg = $"قام المستخدم {match.Groups[1].Value} بتسجيل الدخول";
                 }
                 else
                 {
                     msg = "قام المستخدم بتسجيل الدخول";
                 }
            }
            else if (msg.Contains("created new admin:"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(msg, @"created new admin: (.*)");
                if (match.Success)
                    msg = $"تم إنشاء حساب مشرف جديد باسم {match.Groups[1].Value}";
            }
            else if (msg.Contains("Team") && msg.Contains("activated"))
            {
                msg = "تم تنشيط الفريق";
            }
            else if (msg.Contains("Team") && (msg.Contains("deactivated") || msg.Contains("disabled")))
            {
                msg = "تم تعطيل الفريق";
            }

            // GUID Replacement
            var guidMatches = System.Text.RegularExpressions.Regex.Matches(msg, @"[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}");
            
            foreach (System.Text.RegularExpressions.Match m in guidMatches)
            {
                if (Guid.TryParse(m.Value, out Guid id))
                {
                    string replacement = "عنصر في النظام";
                    if (teams.ContainsKey(id)) replacement = teams[id];
                    else if (tournaments.ContainsKey(id)) replacement = tournaments[id];
                    else if (matchesInfo.ContainsKey(id)) replacement = matchesInfo[id];
                    
                    msg = msg.Replace(m.Value, replacement);
                }
            }

            // Final Polish: Replace known english words if any remain
            if (msg.Contains("User")) msg = msg.Replace("User", "المستخدم");
            if (msg.Contains("Team")) msg = msg.Replace("Team", "الفريق");
            if (msg.Contains("Tournament")) msg = msg.Replace("Tournament", "البطولة");
            if (msg.Contains("Match")) msg = msg.Replace("Match", "المباراة");
            if (msg.Contains("created")) msg = msg.Replace("created", "تم إنشاء");
            if (msg.Contains("updated")) msg = msg.Replace("updated", "تم تحديث");
            if (msg.Contains("deleted")) msg = msg.Replace("deleted", "تم حذف");

            if (msg != activity.Message)
            {
                activity.Message = msg;
                updated = true;
            }

            if (updated)
            {
                await _activityRepository.UpdateAsync(activity, ct);
                anyUpdated = true;
            }
        }

        if (anyUpdated)
        {
            _logger.LogInformation("Activity Log Migration Completed Successfully.");
        }
        else
        {
            _logger.LogInformation("Activity Log Migration: No records needed updating.");
        }
    }
}
