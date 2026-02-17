using System;
using System.Collections.Generic;

namespace Domain.Entities;

public class Team : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Founded { get; set; } = string.Empty;
    public string? City { get; set; }
    public bool IsActive { get; set; } = true;

    // Ownership and Roles are now managed via Player entities with TeamRole (Captain/Member)
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<TeamRegistration> Registrations { get; set; } = new List<TeamRegistration>(); // Tournament registrations
    public ICollection<TeamJoinRequest> JoinRequests { get; set; } = new List<TeamJoinRequest>(); // Player join requests

    public TeamStats? Statistics { get; set; }
}
