using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.ConfirmManualQualification;

/// <summary>
/// Organiser confirms which teams advance from the group stage to the knockout round.
/// Only valid when the tournament is in <c>ManualQualificationPending</c> status
/// (i.e., Manual scheduling mode + group stage fully complete).
///
/// Side-effects (all in one DB round-trip):
///   1. Marks selected <c>TeamRegistration.IsQualifiedForKnockout</c> = true.
///   2. Transitions tournament status → <c>QualificationConfirmed</c>.
///   3. Triggers <see cref="Interfaces.ITournamentLifecycleService.GenerateKnockoutR1Async"/>
///      which seeds round 1 knockout matches and transitions status back to Active.
/// </summary>
public record ConfirmManualQualificationCommand(
    Guid TournamentId,
    ConfirmManualQualificationRequest Request,
    Guid UserId,
    string UserRole) : IRequest<TournamentLifecycleResult>;
