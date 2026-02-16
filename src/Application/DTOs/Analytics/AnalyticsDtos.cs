using System;

namespace Application.DTOs.Analytics;

public class AnalyticsOverview
{
    public int TotalUsers { get; set; }
    public int TotalTeams { get; set; }
    public int ActiveTournaments { get; set; }

    public int MatchesToday { get; set; }
    public int LoginsToday { get; set; }
    public int TotalGoals { get; set; }
}

public class TeamAnalyticsDto
{
    public int PlayerCount { get; set; }
    public int UpcomingMatches { get; set; }
    public int ActiveTournaments { get; set; }
    public string Rank { get; set; } = "-";
}

public class ActivityDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;        // Arabic category
    public string ActionType { get; set; } = string.Empty;  // Code (USER_LOGIN, etc.)
    public string Message { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? UserName { get; set; }
    public string Severity { get; set; } = "info";
    public string? ActorRole { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityName { get; set; }
}

public class ActivityFilterParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? ActorRole { get; set; }
    public string? ActionType { get; set; }
    public string? EntityType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? MinSeverity { get; set; }
    public Guid? UserId { get; set; }
}
