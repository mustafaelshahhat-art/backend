using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Analytics;
using Application.DTOs.Notifications;

namespace Application.Interfaces;

public interface IAnalyticsService
{
    Task<AnalyticsOverview> GetOverviewAsync(Guid? creatorId = null, CancellationToken ct = default);
    Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId, CancellationToken ct = default);
    Task<IEnumerable<ActivityDto>> GetRecentActivitiesAsync(Guid? creatorId = null, CancellationToken ct = default);
    Task LogActivityAsync(string type, string message, Guid? userId = null, string? userName = null, CancellationToken ct = default);
    Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null, CancellationToken ct = default);
}

