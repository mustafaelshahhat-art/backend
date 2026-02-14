using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.RejectRegistration;

public record RejectRegistrationCommand(
    Guid TournamentId, 
    Guid TeamId, 
    RejectRegistrationRequest Request, 
    Guid UserId, 
    string UserRole) : IRequest<TeamRegistrationDto>;
