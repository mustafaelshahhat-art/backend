using System;
using System.Collections.Generic;
using Domain.Enums;

namespace Application.DTOs.Tournaments;

public class TournamentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxTeams { get; set; }
    public int CurrentTeams { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;
    public string Prizes { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string MatchType { get; set; } = string.Empty;
    public int NumberOfGroups { get; set; }
    public int QualifiedTeamsPerGroup { get; set; }
    public string? WalletNumber { get; set; }
    public string? InstaPayNumber { get; set; }
    public bool IsHomeAwayEnabled { get; set; }
    public SeedingMode SeedingMode { get; set; }
    public string? PaymentMethodsJson { get; set; }
    public List<TeamRegistrationDto> Registrations { get; set; } = new();
    public Guid? WinnerTeamId { get; set; }
    public string? WinnerTeamName { get; set; }
}

public class CreateTournamentRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public decimal EntryFee { get; set; }
    public int MaxTeams { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Rules { get; set; } = string.Empty;
    public string Prizes { get; set; } = string.Empty;
    public TournamentFormat Format { get; set; }
    public TournamentLegType MatchType { get; set; }
    public int NumberOfGroups { get; set; }
    public int QualifiedTeamsPerGroup { get; set; }
    public string? WalletNumber { get; set; }
    public string? InstaPayNumber { get; set; }
    public bool IsHomeAwayEnabled { get; set; }
    public SeedingMode SeedingMode { get; set; }
    public string? PaymentMethodsJson { get; set; }
}

public class UpdateTournamentRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? RegistrationDeadline { get; set; }
    public decimal? EntryFee { get; set; }
    public int? MaxTeams { get; set; }
    public string? Location { get; set; }
    public string? Rules { get; set; }
    public string? Prizes { get; set; }
    public TournamentFormat? Format { get; set; }
    public TournamentLegType? MatchType { get; set; }
    public int? NumberOfGroups { get; set; }
    public int? QualifiedTeamsPerGroup { get; set; }
    public string? WalletNumber { get; set; }
    public string? InstaPayNumber { get; set; }
    public bool? IsHomeAwayEnabled { get; set; }
    public SeedingMode? SeedingMode { get; set; }
    public string? PaymentMethodsJson { get; set; }
}

public class TeamRegistrationDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public string CaptainName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PaymentReceiptUrl { get; set; }
    public string? SenderNumber { get; set; }
    public string? RejectionReason { get; set; }
    public string? PaymentMethod { get; set; }
    public DateTime RegisteredAt { get; set; }
    public Guid TournamentId { get; set; }
}

public class SubmitPaymentRequest
{
    public string PaymentReceiptUrl { get; set; } = string.Empty;
    public string? SenderNumber { get; set; }
    public string? PaymentMethod { get; set; }
}

public class RejectRegistrationRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class RegisterTeamRequest
{
    public Guid TeamId { get; set; }
}

public class PendingPaymentResponse
{
    public TournamentDto Tournament { get; set; } = new();
    public TeamRegistrationDto Registration { get; set; } = new();
}

public class TournamentStandingDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamLogoUrl { get; set; }
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; } // Goals Scored
    public int GoalsAgainst { get; set; }
    public int GoalDifference => GoalsFor - GoalsAgainst;
    public int Points { get; set; }
    public int? GroupId { get; set; }
    public List<string> Form { get; set; } = new(); // W, D, L
}

public class GroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class BracketDto
{
    public List<BracketRoundDto> Rounds { get; set; } = new();
}

public class BracketRoundDto
{
    public int RoundNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Application.DTOs.Matches.MatchDto> Matches { get; set; } = new();
}
