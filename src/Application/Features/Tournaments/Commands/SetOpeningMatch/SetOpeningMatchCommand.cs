using MediatR;

namespace Application.Features.Tournaments.Commands.SetOpeningMatch;

/// <summary>
/// PRE-DRAW command to select two teams as the Opening Match.
/// Must be executed BEFORE schedule generation.
/// Overrides any previous opening selection.
/// </summary>
public record SetOpeningMatchCommand(
    Guid TournamentId, 
    Guid HomeTeamId, 
    Guid AwayTeamId, 
    Guid UserId, 
    string UserRole) : IRequest<Unit>;
