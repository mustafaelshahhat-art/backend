using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Tournaments.Commands.SetOpeningMatch;

public record SetOpeningMatchCommand(
    Guid TournamentId, 
    Guid HomeTeamId, 
    Guid AwayTeamId, 
    Guid UserId, 
    string UserRole) : IRequest<IEnumerable<MatchDto>>;
