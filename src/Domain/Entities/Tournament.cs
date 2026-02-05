using System;
using System.Collections.Generic;
using Domain.Enums;

namespace Domain.Entities;

public class Tournament : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
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

    public ICollection<TeamRegistration> Registrations { get; set; } = new List<TeamRegistration>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}
