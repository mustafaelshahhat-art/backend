using Application.DTOs.Tournaments;
using MediatR;
using Application.DTOs.Matches;

namespace Application.Features.Tournaments.Commands.CreateManualKnockoutMatches;

public record CreateManualKnockoutMatchesCommand(
    Guid TournamentId,
    List<KnockoutPairingDto> Pairings,
    Guid UserId,
    string UserRole) : IRequest<IEnumerable<MatchDto>>;
