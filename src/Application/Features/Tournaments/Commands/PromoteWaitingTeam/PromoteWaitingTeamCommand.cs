using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.PromoteWaitingTeam;

public record PromoteWaitingTeamCommand(
    Guid TournamentId, 
    Guid TeamId, 
    Guid UserId, 
    string UserRole) : IRequest<TeamRegistrationDto>;
