using Application.DTOs;
using Application.DTOs.Matches;
using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.CreateManualNextRound;

/// <summary>
/// Submits organiser-supplied pairings for the next knockout round of a Manual-mode tournament.
///
/// Used after <see cref="Application.DTOs.Tournaments.TournamentLifecycleResult.ManualDrawRequired"/>
/// is returned as true by the lifecycle service, signalling that automatic generation was skipped
/// and the organiser must provide the draw.
///
/// Applies to: Round 2, Semi-final.  The Final is ALWAYS generated automatically.
/// </summary>
public record CreateManualNextRoundCommand(
    Guid TournamentId,
    int RoundNumber,
    List<KnockoutPairingDto> Pairings,
    Guid UserId,
    string UserRole) : IRequest<MatchListResponse>;
