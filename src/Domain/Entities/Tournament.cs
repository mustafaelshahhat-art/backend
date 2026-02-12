using System;
using System.Collections.Generic;
using Domain.Enums;

namespace Domain.Entities;

public class Tournament : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? CreatorUserId { get; set; }
    public User? CreatorUser { get; set; }
    // status: "registration_open|active|completed" mapped to logic or Enum?
    // Contract says lower case string, we can use Enum and map.
    // Wait, contract says `status: "registration_open|active|completed"`. 
    // I'll use a string or a custom enum that maps well? DTO has enum string values.
    // I already created MatchStatus. For Tournament, I need a status too. 
    // I missed `TournamentStatus` enum in previous step. I'll add it or use string.
    // Better use string to be compliant exactly or Enum with converter. Best: Enum.
    
    // Adding TournamentStatus Enum property
    public string Status { get; set; } = "draft"; // using string for flexibility or I should define Enum properly.

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxTeams { get; set; }
    public int CurrentTeams { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;
    public string Prizes { get; set; } = string.Empty; // Store as comma-separated or JSON

    // Tournament Format Configurations
    public TournamentFormat Format { get; set; } = TournamentFormat.RoundRobin;
    public TournamentLegType MatchType { get; set; } = TournamentLegType.SingleLeg;
    public int NumberOfGroups { get; set; } = 0;
    public int QualifiedTeamsPerGroup { get; set; } = 0;
    
    // Unified Mode (Consolidated Format + MatchType)
    public TournamentMode? Mode { get; set; }
    public Guid? OpeningMatchId { get; set; }

    // New Configurations
    public bool IsHomeAwayEnabled { get; set; } = false; 
    public SeedingMode SeedingMode { get; set; } = SeedingMode.ShuffleOnly;
    
    // Payment Config (JSON: [{ "type": "E_WALLET", "label": "Orange Cash", "accountNumber": "012..." }])
    public string? PaymentMethodsJson { get; set; }

    // Payment Info (Legacy/Specific overrides if needed, keeping for backward compat)
    public string? WalletNumber { get; set; }
    public string? InstaPayNumber { get; set; }

    public Guid? WinnerTeamId { get; set; }
    public Team? WinnerTeam { get; set; }

    public ICollection<TeamRegistration> Registrations { get; set; } = new List<TeamRegistration>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();

    public TournamentMode GetEffectiveMode()
    {
        if (Mode.HasValue) return Mode.Value;

        // Backward compatibility mapping
        if (Format == TournamentFormat.RoundRobin)
            return IsHomeAwayEnabled || MatchType == TournamentLegType.HomeAndAway ? TournamentMode.LeagueHomeAway : TournamentMode.LeagueSingle;

        if (Format == TournamentFormat.GroupsThenKnockout)
            return TournamentMode.GroupsKnockoutSingle;

        if (Format == TournamentFormat.GroupsWithHomeAwayKnockout)
            return TournamentMode.GroupsKnockoutHomeAway;

        if (Format == TournamentFormat.KnockoutOnly)
            return IsHomeAwayEnabled || MatchType == TournamentLegType.HomeAndAway ? TournamentMode.KnockoutHomeAway : TournamentMode.KnockoutSingle;

        return TournamentMode.LeagueSingle; // Fallback
    }
}
