using MediatR;
using Application.DTOs.Matches;

namespace Application.Features.Tournaments.Commands.SetOpeningMatch;

/// <summary>
/// PRE-DRAW command to select two teams as the Opening Match.
/// Must be executed BEFORE schedule generation.
/// Overrides any previous opening selection.
/// Returns generated matches if in Random scheduling mode.
/// </summary>
public record SetOpeningMatchCommand(
    Guid TournamentId, 
    Guid HomeTeamId, 
    Guid AwayTeamId, 
    Guid UserId, 
    string UserRole) : IRequest<IEnumerable<MatchDto>>;
