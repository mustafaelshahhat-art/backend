using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Matches.Commands.AddMatchEvent;

public record AddMatchEventCommand(
    Guid Id, 
    AddMatchEventRequest Request, 
    Guid UserId, 
    string UserRole) : IRequest<MatchDto>;
