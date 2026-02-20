namespace Application.DTOs.Tournaments;

/// <summary>
/// Result returned by TournamentLifecycleService operations, allowing calling handlers
/// to send notifications and log analytics based on what happened.
/// This breaks the service-to-service chaining by removing INotificationService
/// and IAnalyticsService from the lifecycle service.
/// </summary>
public class TournamentLifecycleResult
{
    public bool TournamentFinalized { get; set; }
    public bool NextRoundGenerated { get; set; }
    public bool GroupsFinished { get; set; }
    public Guid? WinnerTeamId { get; set; }
    public string? WinnerTeamName { get; set; }
    public Guid TournamentId { get; set; }
    public string? TournamentName { get; set; }
    public Guid? CreatorUserId { get; set; }
    public int? RoundNumber { get; set; }
    public int? MatchesGenerated { get; set; }

    // ── Manual Draw Policy ──────────────────────────────────────────────────
    /// <summary>
    /// True when the tournament is in Manual scheduling mode and the next round
    /// must be drawn by the organiser. Automatic generation was skipped.
    /// The UI should display a "Manual Draw" button when this is true.
    /// </summary>
    public bool ManualDrawRequired { get; set; }

    /// <summary>
    /// The round number that requires a manual draw (only set when ManualDrawRequired is true).
    /// </summary>
    public int? ManualDrawRoundNumber { get; set; }

    // ── Manual Qualification Policy ─────────────────────────────────────────
    /// <summary>
    /// True when the group stage just completed AND the tournament is in Manual mode.
    /// Automatic qualification is blocked. The UI must show the qualification selection screen.
    /// Tournament status is now ManualQualificationPending.
    /// </summary>
    public bool ManualQualificationRequired { get; set; }
}
