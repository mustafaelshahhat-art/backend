using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Analytics;
using Application.DTOs.Notifications;

namespace Application.Interfaces;

public interface IAnalyticsService
{
    Task<AnalyticsOverview> GetOverviewAsync();
    Task<IEnumerable<ActivityDto>> GetRecentActivitiesAsync();
    Task LogActivityAsync(string type, string message, Guid? userId = null, string? userName = null);
}

