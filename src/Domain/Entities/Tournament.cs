using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Domain.Enums;

namespace Domain.Entities;

public class Tournament : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? NameEn { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public Guid? CreatorUserId { get; set; }
    public User? CreatorUser { get; set; }
    
    // PROD-HARDEN: Locked down Status to prevent direct mutation
    public TournamentStatus Status { get; private set; } = TournamentStatus.Draft;

    public void ChangeStatus(TournamentStatus newStatus)
    {
        ValidateTransition(newStatus);
        Status = newStatus;
    }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxTeams { get; set; }
    public int? MinTeams { get; set; }
    public int CurrentTeams { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;
    public string Prizes { get; set; } = string.Empty;

    // Tournament Format Configurations
    public TournamentFormat Format { get; set; } = TournamentFormat.RoundRobin;
    public TournamentLegType MatchType { get; set; } = TournamentLegType.SingleLeg;
    public int NumberOfGroups { get; set; } = 0;
    public int QualifiedTeamsPerGroup { get; set; } = 0;
    
    // Unified Mode (Consolidated Format + MatchType)
    public TournamentMode? Mode { get; set; }
    public Guid? OpeningMatchId { get; set; }

    // ============================================================
    // OPENING MATCH PRE-DRAW SELECTION
    // These two teams will be placed in the same group automatically
    // and excluded from shuffle/random draw.
    // ============================================================
    public Guid? OpeningTeamAId { get; private set; }
    public Guid? OpeningTeamBId { get; private set; }

    // Backward-compatible aliases (mapped to same DB columns)
    [NotMapped]
    public Guid? OpeningMatchHomeTeamId
    {
        get => OpeningTeamAId;
        set => OpeningTeamAId = value;
    }
    
    [NotMapped]
    public Guid? OpeningMatchAwayTeamId
    {
        get => OpeningTeamBId;
        set => OpeningTeamBId = value;
    }

    /// <summary>
    /// PRE-DRAW: Set the two teams for the opening match.
    /// Must be called BEFORE schedule generation.
    /// Validates: teams belong to tournament, are registered, are different,
    /// no matches generated, tournament not Active, status is RegistrationClosed.
    /// </summary>
    public void SetOpeningTeams(Guid teamAId, Guid teamBId, IEnumerable<Guid> registeredTeamIds, bool matchesExist)
    {
        if (teamAId == teamBId)
            throw new InvalidOperationException("لا يمكن اختيار نفس الفريق للمباراة الافتتاحية.");

        if (Status != TournamentStatus.RegistrationClosed && Status != TournamentStatus.WaitingForOpeningMatchSelection)
            throw new InvalidOperationException("يمكن تحديد المباراة الافتتاحية فقط عندما يكون التسجيل مغلقاً أو بانتظار اختيار المباراة الافتتاحية.");

        if (matchesExist)
            throw new InvalidOperationException("لا يمكن تحديد المباراة الافتتاحية بعد إنشاء المباريات. قم بإعادة تعيين الجدول أولاً.");

        if (SchedulingMode != SchedulingMode.Random && SchedulingMode != SchedulingMode.Manual)
            throw new InvalidOperationException("وضع الجدولة غير مدعوم لتحديد المباراة الافتتاحية.");

        var registeredSet = registeredTeamIds.ToHashSet();
        if (!registeredSet.Contains(teamAId))
            throw new InvalidOperationException("الفريق الأول غير مسجل في البطولة.");

        if (!registeredSet.Contains(teamBId))
            throw new InvalidOperationException("الفريق الثاني غير مسجل في البطولة.");

        OpeningTeamAId = teamAId;
        OpeningTeamBId = teamBId;
    }

    /// <summary>
    /// Clears the opening team selection (used during schedule reset).
    /// </summary>
    public void ClearOpeningTeams()
    {
        OpeningTeamAId = null;
        OpeningTeamBId = null;
    }

    /// <summary>
    /// Returns true if opening teams have been selected.
    /// </summary>
    public bool HasOpeningTeams => OpeningTeamAId.HasValue && OpeningTeamBId.HasValue;

    // Late Registration Policy
    public bool AllowLateRegistration { get; set; } = false;
    public LateRegistrationMode LateRegistrationMode { get; set; } = LateRegistrationMode.None;

    // New Configurations
    public bool IsHomeAwayEnabled { get; set; } = false; 
    
    // Scheduling Mode: Random or Manual
    public SchedulingMode SchedulingMode { get; set; } = SchedulingMode.Random;
    
    // Payment Config
    public string? PaymentMethodsJson { get; set; }

    // Payment Info (Legacy)
    public string? WalletNumber { get; set; }
    public string? InstaPayNumber { get; set; }

    public Guid? WinnerTeamId { get; set; }
    public Team? WinnerTeam { get; set; }

    public ICollection<TeamRegistration> Registrations { get; set; } = new List<TeamRegistration>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();
    public ICollection<TournamentPlayer> TournamentPlayers { get; set; } = new List<TournamentPlayer>();

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

    public void ValidateTransition(TournamentStatus newStatus)
    {
        if (Status == newStatus) return;

        bool isValid = false;
        switch (Status)
        {
            case TournamentStatus.Draft:
                isValid = newStatus == TournamentStatus.RegistrationOpen || newStatus == TournamentStatus.Cancelled;
                break;

            case TournamentStatus.RegistrationOpen:
                isValid = newStatus == TournamentStatus.RegistrationClosed || newStatus == TournamentStatus.Cancelled;
                break;

            case TournamentStatus.RegistrationClosed:
                isValid = newStatus == TournamentStatus.Active || 
                          newStatus == TournamentStatus.WaitingForOpeningMatchSelection || 
                          newStatus == TournamentStatus.Cancelled;
                break;

            case TournamentStatus.WaitingForOpeningMatchSelection:
                isValid = newStatus == TournamentStatus.Active || newStatus == TournamentStatus.Cancelled;
                break;

            case TournamentStatus.Active:
                isValid = newStatus == TournamentStatus.Completed || 
                          newStatus == TournamentStatus.WaitingForOpeningMatchSelection ||
                          newStatus == TournamentStatus.Cancelled;
                break;

            case TournamentStatus.Completed:
            case TournamentStatus.Cancelled:
                isValid = false;
                break;
        }

        if (!isValid)
        {
            throw new InvalidOperationException($"فشل تغيير الحالة: لا يمكن الانتقال من {Status} إلى {newStatus}.");
        }
    }
}
