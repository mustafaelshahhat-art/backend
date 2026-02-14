using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Matches.Commands.RemoveMatchEvent;

public record RemoveMatchEventCommand(
    Guid MatchId, 
    Guid EventId, 
    Guid UserId, 
    string UserRole) : IRequest<MatchDto>;
