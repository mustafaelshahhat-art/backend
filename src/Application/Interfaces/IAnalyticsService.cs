using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Analytics;

namespace Application.Interfaces;

public interface IAnalyticsService
{
    Task<AnalyticsOverview> GetOverviewAsync(Guid? creatorId = null, CancellationToken ct = default);
    Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(Guid teamId, CancellationToken ct = default);

    Task<Application.Common.Models.PagedResult<ActivityDto>> GetRecentActivitiesAsync(
        ActivityFilterParams filters, Guid? creatorId = null, CancellationToken ct = default);

    // Original logging (backward compat)
    Task LogActivityAsync(string type, string message, Guid? userId = null, string? userName = null, CancellationToken ct = default);
    Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId = null, string? userName = null, CancellationToken ct = default);

    // Enriched logging with entity context
    Task LogActivityAsync(string type, string message, Guid? userId, string? userName,
        string? actorRole, Guid? entityId, string? entityType, string? entityName, string? metadata, CancellationToken ct = default);
    Task LogActivityByTemplateAsync(string code, Dictionary<string, string> placeholders, Guid? userId, string? userName,
        string? actorRole, Guid? entityId, string? entityType, string? entityName, string? metadata, CancellationToken ct = default);
}

