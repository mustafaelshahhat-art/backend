using System;

namespace Domain.Entities;

public class TeamJoinRequest : BaseEntity
{
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Status { get; set; } = "pending"; // pending|approved|rejected
}
