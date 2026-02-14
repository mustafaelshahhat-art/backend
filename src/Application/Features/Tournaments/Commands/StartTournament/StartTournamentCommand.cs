using Application.DTOs.Tournaments;
using MediatR;

namespace Application.Features.Tournaments.Commands.StartTournament;

public record StartTournamentCommand(Guid Id, Guid UserId, string UserRole) : IRequest<TournamentDto>;
