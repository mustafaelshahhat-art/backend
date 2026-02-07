using System;
using Domain.Enums;

namespace Domain.Entities;

public class TeamRegistration : BaseEntity
{
    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public Guid TeamId { get; set; }
    public Team? Team { get; set; }

    public RegistrationStatus Status { get; set; } = RegistrationStatus.PendingPaymentReview;
    public string? PaymentReceiptUrl { get; set; }
    public string? SenderNumber { get; set; }
    public string? RejectionReason { get; set; }
}
