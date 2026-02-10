using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Analytics;
using Application.DTOs.Notifications;

namespace Application.Interfaces;

public interface IAnalyticsService
{
    Task<AnalyticsOverview> GetOverviewAsync(Guid? creatorId = null);
    Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId);
    Task<IEnumerable<ActivityDto>> GetRecentActivitiesAsync();
    Task LogActivityAsync(string type, string message, Guid? userId = null, string? userName = null);
    Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null);
}

