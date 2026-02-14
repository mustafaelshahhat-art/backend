using Application.DTOs.Tournaments;
using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Tournaments.Commands.ManualDraw;

public record ManualDrawCommand(
    Guid TournamentId, 
    ManualDrawRequest Request, 
    Guid UserId, 
    string UserRole) : IRequest<IEnumerable<MatchDto>>;
