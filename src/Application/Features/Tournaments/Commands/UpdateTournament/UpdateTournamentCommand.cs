using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.UpdateTournament;

public record UpdateTournamentCommand(
    Guid Id, 
    UpdateTournamentRequest Request, 
    Guid UserId, 
    string UserRole) : IRequest<TournamentDto>;
