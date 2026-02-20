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
    public string? PaymentMethod { get; set; } // "E_WALLET" | "INSTAPAY"
    public string? SenderNumber { get; set; }
    public string? RejectionReason { get; set; }
    public int? GroupId { get; set; }

    /// <summary>
    /// Set to true by <c>ConfirmManualQualificationCommandHandler</c> when the organiser
    /// explicitly selects this team to advance from the group stage to the knockout round.
    ///
    /// Only meaningful for Manual-mode Group+Knockout tournaments.
    /// In Automatic mode this flag is never set and the lifecycle service uses standings.
    /// </summary>
    public bool IsQualifiedForKnockout { get; set; } = false;
}
