using Application.DTOs.Matches;
using MediatR;

namespace Application.Features.Matches.Commands.UpdateMatch;

public record UpdateMatchCommand(
    Guid Id, 
    UpdateMatchRequest Request, 
    Guid UserId, 
    string UserRole) : IRequest<MatchDto>;
