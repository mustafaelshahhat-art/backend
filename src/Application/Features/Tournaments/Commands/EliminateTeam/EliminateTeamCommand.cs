using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.EliminateTeam;

public record EliminateTeamCommand(Guid TournamentId, Guid TeamId, Guid UserId, string UserRole) : IRequest<bool>;
