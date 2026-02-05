using System;
using System.Collections.Generic;

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
    public List<TeamRegistrationDto> Registrations { get; set; } = new();
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
}

public class TeamRegistrationDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string CaptainName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? PaymentReceiptUrl { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime RegisteredAt { get; set; }
    public Guid TournamentId { get; set; }
}

public class SubmitPaymentRequest
{
    public string PaymentReceiptUrl { get; set; } = string.Empty;
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
    public int Played { get; set; }
    public int Won { get; set; }
    public int Drawn { get; set; }
    public int Lost { get; set; }
    public int TodoGoalsFor { get; set; } // Goals Scored
    public int GoalsAgainst { get; set; }
    public int GoalDifference => TodoGoalsFor - GoalsAgainst;
    public int Points { get; set; }
    public List<string> Form { get; set; } = new(); // W, D, L
}
