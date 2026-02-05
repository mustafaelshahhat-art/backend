using System;

namespace Application.DTOs.Analytics;

public class AnalyticsOverview
{
    public int TotalUsers { get; set; }
    public int TotalTeams { get; set; }
    public int ActiveTournaments { get; set; }
    public int PendingObjections { get; set; }
    public int MatchesToday { get; set; }
}

public class ActivityDto
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? UserName { get; set; }
}
