using System;

namespace Domain.Entities;

public class TournamentPlayer : BaseEntity
{
    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public Guid PlayerId { get; set; }
    public Player? Player { get; set; }

    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public Guid RegistrationId { get; set; }
    public TeamRegistration? Registration { get; set; }
}
