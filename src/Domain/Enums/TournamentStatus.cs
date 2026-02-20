namespace Domain.Enums;

public enum TournamentStatus
{
    Draft,
    RegistrationOpen,
    RegistrationClosed,
    Active,
    WaitingForOpeningMatchSelection,
    Completed,
    Cancelled,
    /// <summary>
    /// Group stage is complete. Manual-mode tournament is waiting for the
    /// organiser to select which teams advance to the knockout round.
    /// Automatic generation is BLOCKED until this is resolved.
    /// </summary>
    ManualQualificationPending,
    /// <summary>
    /// Organiser has confirmed the qualified teams. Knockout Round 1 can now
    /// be generated using the pre-selected IsQualifiedForKnockout teams.
    /// </summary>
    QualificationConfirmed,
}
